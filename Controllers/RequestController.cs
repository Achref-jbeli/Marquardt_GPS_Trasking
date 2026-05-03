using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GpsAdminServer.Models;
using GpsAdminServer.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace GpsAdminServer.Controllers;

public class RequestController : Controller
{
    private readonly GpsDbContext _dbContext;
    private readonly AuthService _authService;
    private readonly NotificationService _notificationService;

    public RequestController(
        GpsDbContext dbContext,
        AuthService authService,
        NotificationService notificationService)
    {
        _dbContext = dbContext;
        _authService = authService;
        _notificationService = notificationService;
    }

    // ==================== DASHBOARD ====================

    public async Task<IActionResult> Dashboard()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        // Récupérer le superviseur
        ApplicationUser? supervisor = null;
        if (user.SupervisorId.HasValue && user.SupervisorId.Value > 0)
        {
            supervisor = await _dbContext.Users.FindAsync(user.SupervisorId.Value);
        }

        int pendingRequests, approvedRequests, completedRequests, rejectedRequests;

        if (user.Role == "Admin")
        {
            // Admin voit les statistiques GLOBALES
            pendingRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "Pending");
            approvedRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "SupervisorApproved" || r.Status == "AdminApproved");
            completedRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "Completed");
            rejectedRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "Rejected");
        }
        else
        {
            // Utilisateur voit ses propres statistiques
            pendingRequests = await _dbContext.TravelRequests.CountAsync(r => r.EmployeeId == user.Id && r.Status == "Pending");
            approvedRequests = await _dbContext.TravelRequests.CountAsync(r => r.EmployeeId == user.Id && (r.Status == "SupervisorApproved" || r.Status == "AdminApproved"));
            completedRequests = await _dbContext.TravelRequests.CountAsync(r => r.EmployeeId == user.Id && r.Status == "Completed");
            rejectedRequests = await _dbContext.TravelRequests.CountAsync(r => r.EmployeeId == user.Id && r.Status == "Rejected");
        }

        ViewBag.PendingRequests = pendingRequests;
        ViewBag.ApprovedRequests = approvedRequests;
        ViewBag.CompletedRequests = completedRequests;
        ViewBag.RejectedRequests = rejectedRequests;
        ViewBag.User = user;
        ViewBag.Supervisor = supervisor;

        return View();
    }

    // ==================== GESTION DES DEMANDES ====================

    [HttpGet]
    public IActionResult CreateRequest()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        // Admin ne peut pas créer de demande
        if (user.Role == "Admin")
        {
            TempData["Error"] = "L'administrateur ne peut pas créer de demandes de voyage";
            return RedirectToAction("Dashboard");
        }

        ViewBag.User = user;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateRequest(TravelRequest request)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        // Vérifier que la date + heure est dans le futur
        var arrivalDateTime = request.TravelDate.Date + request.ArrivalTime.TimeOfDay;

        if (arrivalDateTime <= DateTime.Now)
        {
            ViewBag.Error = "La date et l'heure d'arrivée doivent être dans le futur";
            ViewBag.User = user;
            return View(request);
        }

        request.EmployeeId = user.Id;
        request.EmployeeName = user.FullName;
        request.SupervisorId = user.SupervisorId;
        request.Status = "Pending";
        request.CreatedAt = DateTime.Now;

        _dbContext.TravelRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        // Notifier le superviseur s'il existe
        if (user.SupervisorId.HasValue)
        {
            await _notificationService.SendNotification(
                user.SupervisorId.Value,
                "📋 Nouvelle demande de voyage",
                $"{user.FullName} a fait une demande pour {request.Destination} le {request.TravelDate:dd/MM/yyyy}",
                "Request",
                request.Id
            );
        }

        TempData["Success"] = "Demande envoyée avec succès !";
        return RedirectToAction("MyRequests");
    }

    [HttpGet]
    public async Task<IActionResult> MyRequests()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        List<TravelRequest> requests;

        if (user.Role == "Admin")
        {
            // Admin voit TOUTES les demandes des utilisateurs
            requests = await _dbContext.TravelRequests
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
        else
        {
            // Utilisateur voit seulement ses demandes
            requests = await _dbContext.TravelRequests
                .Where(r => r.EmployeeId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        ViewBag.User = user;
        return View(requests);
    }

    [HttpGet]
    public async Task<IActionResult> RequestDetails(int id)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        var request = await _dbContext.TravelRequests
            .FirstOrDefaultAsync(r => r.Id == id &&
                (r.EmployeeId == user.Id || r.SupervisorId == user.Id || user.Role == "Admin"));

        if (request == null) return NotFound();

        if (request.VehicleId.HasValue)
        {
            ViewBag.Vehicle = await _dbContext.Vehicles.FindAsync(request.VehicleId.Value);
        }

        ViewBag.User = user;
        return View(request);
    }

    [HttpPost]
    public async Task<IActionResult> CancelRequest(int id)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        var request = await _dbContext.TravelRequests.FindAsync(id);
        if (request == null || request.EmployeeId != user.Id) return NotFound();

        if (request.Status == "Pending")
        {
            request.Status = "Cancelled";
            await _dbContext.SaveChangesAsync();
            TempData["Success"] = "Demande annulée avec succès";
        }
        else
        {
            TempData["Error"] = "Impossible d'annuler une demande déjà traitée";
        }

        return RedirectToAction("MyRequests");
    }

    // ==================== APPROBATIONS ====================

    [HttpGet]
    public async Task<IActionResult> PendingApprovals()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        List<TravelRequest> requests = new List<TravelRequest>();

        if (user.Role == "Admin")
        {
            // Admin voit TOUTES les demandes en attente
            requests = await _dbContext.TravelRequests
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
        else
        {
            // Utilisateur voit seulement les demandes des employés dont il est le responsable
            requests = await _dbContext.TravelRequests
                .Where(r => r.SupervisorId == user.Id && r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        ViewBag.User = user;
        return View(requests);
    }

    [HttpGet]
    public async Task<IActionResult> ApprovedByMe()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        List<TravelRequest> requests = new List<TravelRequest>();

        if (user.Role == "Admin")
        {
            // Admin voit toutes les demandes approuvées
            requests = await _dbContext.TravelRequests
                .Where(r => r.Status == "SupervisorApproved" || r.Status == "AdminApproved")
                .OrderByDescending(r => r.AdminApprovedAt ?? r.SupervisorApprovedAt)
                .ToListAsync();
        }
        else
        {
            // Utilisateur voit les demandes qu'il a approuvées
            requests = await _dbContext.TravelRequests
                .Where(r => r.SupervisorId == user.Id && (r.Status == "SupervisorApproved" || r.Status == "AdminApproved"))
                .OrderByDescending(r => r.SupervisorApprovedAt)
                .ToListAsync();
        }

        ViewBag.User = user;
        return View(requests);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveRequest(int id, string comments)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        var request = await _dbContext.TravelRequests.FindAsync(id);
        if (request == null) return NotFound();

        if (user.Role != "Admin" && request.SupervisorId != user.Id)
        {
            TempData["Error"] = "Vous n'êtes pas autorisé à approuver cette demande";
            return RedirectToAction("PendingApprovals");
        }

        if (request.Status != "Pending")
        {
            TempData["Error"] = "Cette demande a déjà été traitée";
            return RedirectToAction("PendingApprovals");
        }

        if (user.Role == "Admin")
        {
            request.Status = "AdminApproved";
            request.AdminComments = comments;
            request.AdminApprovedAt = DateTime.Now;
        }
        else
        {
            request.Status = "SupervisorApproved";
            request.SupervisorComments = comments;
            request.SupervisorApprovedAt = DateTime.Now;
        }

        await _dbContext.SaveChangesAsync();

        // Notifier l'employé
        await _notificationService.SendNotification(
            request.EmployeeId,
            "✅ Demande approuvée",
            $"Votre demande a été approuvée par {user.FullName}",
            "Approval",
            request.Id
        );

        TempData["Success"] = "Demande approuvée avec succès";
        return RedirectToAction("PendingApprovals");
    }

    [HttpPost]
    public async Task<IActionResult> RejectRequest(int id, string comments)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        var request = await _dbContext.TravelRequests.FindAsync(id);
        if (request == null) return NotFound();

        if (user.Role != "Admin" && request.SupervisorId != user.Id)
        {
            TempData["Error"] = "Vous n'êtes pas autorisé à refuser cette demande";
            return RedirectToAction("PendingApprovals");
        }

        if (request.Status != "Pending")
        {
            TempData["Error"] = "Cette demande a déjà été traitée";
            return RedirectToAction("PendingApprovals");
        }

        request.Status = "Rejected";
        if (user.Role == "Admin")
        {
            request.AdminComments = comments;
            request.AdminApprovedAt = DateTime.Now;
        }
        else
        {
            request.SupervisorComments = comments;
            request.SupervisorApprovedAt = DateTime.Now;
        }

        await _dbContext.SaveChangesAsync();

        // Notifier l'employé
        await _notificationService.SendNotification(
            request.EmployeeId,
            "❌ Demande refusée",
            $"Votre demande a été refusée par {user.FullName}. Raison: {comments}",
            "Rejection",
            request.Id
        );

        TempData["Success"] = "Demande refusée";
        return RedirectToAction("PendingApprovals");
    }

    // ==================== NOTIFICATIONS ====================

    [HttpGet]
    public async Task<IActionResult> Notifications()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        // Marquer comme lues
        var unreadNotifications = notifications.Where(n => !n.IsRead).ToList();
        foreach (var notif in unreadNotifications)
        {
            notif.IsRead = true;
        }
        await _dbContext.SaveChangesAsync();

        ViewBag.User = user;
        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> GetNotificationCount()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return Json(new { count = 0 });

        var count = await _dbContext.Notifications
            .CountAsync(n => n.UserId == user.Id && !n.IsRead);

        return Json(new { count = count });
    }

    [HttpPost]
    public async Task<IActionResult> MarkNotificationAsRead(int id)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return Unauthorized();

        var notification = await _dbContext.Notifications.FindAsync(id);
        if (notification != null && notification.UserId == user.Id)
        {
            notification.IsRead = true;
            await _dbContext.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllNotificationsAsRead()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return Unauthorized();

        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .ToListAsync();

        foreach (var n in notifications)
        {
            n.IsRead = true;
        }
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    // ==================== PROFIL UTILISATEUR ====================

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        ApplicationUser? supervisor = null;
        if (user.SupervisorId.HasValue && user.SupervisorId.Value > 0)
        {
            supervisor = await _dbContext.Users.FindAsync(user.SupervisorId.Value);
        }

        ViewBag.Supervisor = supervisor;
        ViewBag.User = user;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile(string fullName, string email)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        user.FullName = fullName;
        user.Email = email;

        await _dbContext.SaveChangesAsync();

        TempData["Success"] = "Profil mis à jour avec succès";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        if (user.Password != currentPassword)
        {
            TempData["Error"] = "Mot de passe actuel incorrect";
            return RedirectToAction("Profile");
        }

        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "Les nouveaux mots de passe ne correspondent pas";
            return RedirectToAction("Profile");
        }

        if (newPassword.Length < 4)
        {
            TempData["Error"] = "Le mot de passe doit contenir au moins 4 caractères";
            return RedirectToAction("Profile");
        }

        user.Password = newPassword;
        await _dbContext.SaveChangesAsync();

        TempData["Success"] = "Mot de passe changé avec succès";
        return RedirectToAction("Profile");
    }

    // ==================== STATISTIQUES UTILISATEUR ====================

    [HttpGet]
    public async Task<IActionResult> MyStatistics()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return RedirectToAction("Login", "Account");

        int totalRequests, pendingRequests, approvedRequests, completedRequests, rejectedRequests;

        if (user.Role == "Admin")
        {
            // Admin voit les statistiques GLOBALES
            totalRequests = await _dbContext.TravelRequests.CountAsync();
            pendingRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "Pending");
            approvedRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "SupervisorApproved" || r.Status == "AdminApproved");
            completedRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "Completed");
            rejectedRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "Rejected");
        }
        else
        {
            // Utilisateur voit ses propres statistiques
            totalRequests = await _dbContext.TravelRequests.CountAsync(r => r.EmployeeId == user.Id);
            pendingRequests = await _dbContext.TravelRequests.CountAsync(r => r.EmployeeId == user.Id && r.Status == "Pending");
            approvedRequests = await _dbContext.TravelRequests.CountAsync(r => r.EmployeeId == user.Id && (r.Status == "SupervisorApproved" || r.Status == "AdminApproved"));
            completedRequests = await _dbContext.TravelRequests.CountAsync(r => r.EmployeeId == user.Id && r.Status == "Completed");
            rejectedRequests = await _dbContext.TravelRequests.CountAsync(r => r.EmployeeId == user.Id && r.Status == "Rejected");
        }

        var recentRequests = await _dbContext.TravelRequests
            .Where(r => r.EmployeeId == user.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync();

        ViewBag.TotalRequests = totalRequests;
        ViewBag.PendingRequests = pendingRequests;
        ViewBag.ApprovedRequests = approvedRequests;
        ViewBag.CompletedRequests = completedRequests;
        ViewBag.RejectedRequests = rejectedRequests;
        ViewBag.RecentRequests = recentRequests;
        ViewBag.User = user;

        return View();
    }
}