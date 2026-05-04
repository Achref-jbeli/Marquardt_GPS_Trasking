using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GpsAdminServer.Models;
using GpsAdminServer.Services;
using System.Threading.Tasks;
using System;

namespace GpsAdminServer.Controllers;

public class AdminController : Controller
{
    private readonly GpsDbContext _dbContext;
    private readonly AuthService _authService;
    private readonly TravelService _travelService;
    private readonly NotificationService _notificationService;

    public AdminController(
        GpsDbContext dbContext,
        AuthService authService,
        TravelService travelService,
        NotificationService notificationService)
    {
        _dbContext = dbContext;
        _authService = authService;
        _travelService = travelService;
        _notificationService = notificationService;
    }

    // ==================== TEST AUTH ====================

    public IActionResult TestAuth()
    {
        var user = _authService.GetCurrentUser();
        var isAdmin = _authService.IsAdmin();
        var sessionRole = HttpContext.Session.GetString("UserRole");

        return Content($"User: {user?.Username}, Role DB: {user?.Role}, IsAdmin: {isAdmin}, SessionRole: {sessionRole}");
    }

    // ==================== DASHBOARD PRINCIPAL ====================

    public IActionResult Index()
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");
        return RedirectToAction("Dashboard");
    }

    public async Task<IActionResult> Dashboard()
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        // Statistiques
        var pendingRequests = await _dbContext.TravelRequests
            .CountAsync(r => r.Status == "Pending");

        var supervisorApprovedRequests = await _dbContext.TravelRequests
            .CountAsync(r => r.Status == "SupervisorApproved");

        var completedRequests = await _dbContext.TravelRequests
            .CountAsync(r => r.Status == "Completed");

        var activeVehicles = await _dbContext.Vehicles
            .CountAsync(v => v.Status == "Available");

        var totalDrivers = await _dbContext.Chauffeurs.CountAsync();
        var totalUsers = await _dbContext.Users.CountAsync();

        // Demandes récentes
        var latestRequests = await _dbContext.TravelRequests
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync();

        ViewBag.PendingRequests = pendingRequests;
        ViewBag.SupervisorApprovedRequests = supervisorApprovedRequests;
        ViewBag.CompletedRequests = completedRequests;
        ViewBag.ActiveVehicles = activeVehicles;
        ViewBag.TotalDrivers = totalDrivers;
        ViewBag.TotalUsers = totalUsers;
        ViewBag.LatestRequests = latestRequests;

        return View();
    }

    // ==================== GESTION DES UTILISATEURS ====================

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var currentUser = _authService.GetCurrentUser();
        var users = await _dbContext.Users.ToListAsync();
        var supervisors = users.Where(u => u.Role != "Admin").ToList();

        ViewBag.Users = users;
        ViewBag.Supervisors = supervisors;
        ViewBag.CurrentUserId = currentUser?.Id ?? 0;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> AddUser()
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var allUsers = await _dbContext.Users.ToListAsync();
        ViewBag.Users = allUsers;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> AddUser(ApplicationUser user, int? supervisorId)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        // Vérifier si l'utilisateur existe déjà
        if (await _dbContext.Users.AnyAsync(u => u.Username == user.Username))
        {
            ViewBag.Error = "Ce nom d'utilisateur existe déjà";
            ViewBag.Users = await _dbContext.Users.ToListAsync();
            return View(user);
        }

        user.CreatedAt = DateTime.Now;
        user.IsActive = true;

        // Seuls les utilisateurs normaux peuvent avoir un superviseur
        if (user.Role == "User")
        {
            user.SupervisorId = supervisorId;
        }
        else
        {
            user.SupervisorId = null;
        }

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        TempData["Success"] = $"L'utilisateur {user.FullName} a été ajouté avec succès";
        return RedirectToAction("Users");
    }

    [HttpPost]
    public async Task<IActionResult> EditUser(int id, string fullName, string username, string email, string department, string role, int? supervisorId)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var user = await _dbContext.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.FullName = fullName;
        user.Username = username;
        user.Email = email;
        user.Department = department;
        user.Role = role;

        if (role == "User")
        {
            user.SupervisorId = supervisorId;
        }
        else
        {
            user.SupervisorId = null;
        }

        await _dbContext.SaveChangesAsync();
        TempData["Success"] = "Utilisateur modifié avec succès";

        return RedirectToAction("Users");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var currentUser = _authService.GetCurrentUser();

        // Empêcher de se supprimer soi-même
        if (currentUser != null && currentUser.Id == id)
        {
            TempData["Error"] = "Vous ne pouvez pas supprimer votre propre compte";
            return RedirectToAction("Users");
        }

        var user = await _dbContext.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Supprimer les notifications associées
        var notifications = _dbContext.Notifications.Where(n => n.UserId == id);
        _dbContext.Notifications.RemoveRange(notifications);

        // Supprimer les demandes associées
        var requests = _dbContext.TravelRequests.Where(r => r.EmployeeId == id);
        _dbContext.TravelRequests.RemoveRange(requests);

        // Mettre à jour les employés qui avaient ce superviseur
        var employees = _dbContext.Users.Where(u => u.SupervisorId == id);
        foreach (var emp in employees)
        {
            emp.SupervisorId = null;
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();

        TempData["Success"] = $"L'utilisateur {user.FullName} a été supprimé avec succès";
        return RedirectToAction("Users");
    }

    // ==================== GESTION DES DEMANDES ====================

    [HttpGet]
    public async Task<IActionResult> Requests()
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var requests = await _dbContext.TravelRequests
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var vehicles = await _dbContext.Vehicles.Where(v => v.Status == "Available").ToListAsync();

        ViewBag.Vehicles = vehicles;
        return View(requests);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveRequest(int id, string comments, int? vehicleId)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var request = await _dbContext.TravelRequests.FindAsync(id);
        if (request == null) return NotFound();

        request.Status = "AdminApproved";
        request.AdminComments = comments;
        request.AdminApprovedAt = DateTime.Now;
        request.VehicleId = vehicleId;

        // Changer le statut du véhicule
        if (vehicleId.HasValue)
        {
            var vehicle = await _dbContext.Vehicles.FindAsync(vehicleId.Value);
            if (vehicle != null)
            {
                vehicle.Status = "InUse";
                vehicle.CurrentRequestId = request.Id;
                vehicle.LastUsedAt = DateTime.Now;
            }
        }

        await _dbContext.SaveChangesAsync();

        // Notifier l'employé
        await _notificationService.SendNotification(
            request.EmployeeId,
            "✅ Demande approuvée",
            $"Votre demande de voyage a été approuvée par l'administrateur",
            "Approval",
            request.Id
        );

        // Notifier le superviseur
        if (request.SupervisorId.HasValue)
        {
            await _notificationService.SendNotification(
                request.SupervisorId.Value,
                "✅ Demande approuvée",
                $"La demande de {request.EmployeeName} a été approuvée",
                "Approval",
                request.Id
            );
        }

        TempData["Success"] = "Demande approuvée avec succès";
        return RedirectToAction("Requests");
    }

    [HttpPost]
    public async Task<IActionResult> RejectRequest(int id, string comments)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var request = await _dbContext.TravelRequests.FindAsync(id);
        if (request == null) return NotFound();

        request.Status = "Rejected";
        request.AdminComments = comments;

        await _dbContext.SaveChangesAsync();

        // Notifier l'employé
        await _notificationService.SendNotification(
            request.EmployeeId,
            "❌ Demande refusée",
            $"Votre demande a été refusée. Raison: {comments}",
            "Rejection",
            request.Id
        );

        TempData["Success"] = "Demande refusée";
        return RedirectToAction("Requests");
    }

    [HttpPost]
    public async Task<IActionResult> CompleteRequest(int id)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var request = await _dbContext.TravelRequests.FindAsync(id);
        if (request == null) return NotFound();

        request.Status = "Completed";
        request.CompletedAt = DateTime.Now;

        // Libérer le véhicule
        if (request.VehicleId.HasValue)
        {
            var vehicle = await _dbContext.Vehicles.FindAsync(request.VehicleId.Value);
            if (vehicle != null)
            {
                vehicle.Status = "Available";
                vehicle.CurrentRequestId = null;
            }
        }

        await _dbContext.SaveChangesAsync();

        TempData["Success"] = "Voyage marqué comme terminé";
        return RedirectToAction("Requests");
    }

    // ==================== GESTION DES VÉHICULES ====================

    [HttpGet]
    public async Task<IActionResult> Vehicles()
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var vehicles = await _dbContext.Vehicles.ToListAsync();
        return View(vehicles);
    }

    [HttpPost]
    public async Task<IActionResult> AddVehicle(Vehicle vehicle)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        vehicle.CreatedAt = DateTime.Now;
        vehicle.Status = "Available";
        _dbContext.Vehicles.Add(vehicle);
        await _dbContext.SaveChangesAsync();

        TempData["Success"] = $"Véhicule {vehicle.PlateNumber} ajouté avec succès";
        return RedirectToAction("Vehicles");
    }

    [HttpPost]
    public async Task<IActionResult> EditVehicle(int id, string plateNumber, string brand, string model, string year, string status, string chauffeurId)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var vehicle = await _dbContext.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        vehicle.PlateNumber = plateNumber;
        vehicle.Brand = brand;
        vehicle.Model = model;
        vehicle.Year = year;
        vehicle.Status = status;
        vehicle.ChauffeurId = chauffeurId;

        await _dbContext.SaveChangesAsync();

        TempData["Success"] = "Véhicule modifié avec succès";
        return RedirectToAction("Vehicles");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateVehicleStatus(int id, string status)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var vehicle = await _dbContext.Vehicles.FindAsync(id);
        if (vehicle != null)
        {
            vehicle.Status = status;
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToAction("Vehicles");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteVehicle(int id)
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var vehicle = await _dbContext.Vehicles.FindAsync(id);
        if (vehicle != null)
        {
            _dbContext.Vehicles.Remove(vehicle);
            await _dbContext.SaveChangesAsync();
            TempData["Success"] = "Véhicule supprimé avec succès";
        }

        return RedirectToAction("Vehicles");
    }

    // ==================== TRAFIC ET HISTORIQUE ====================

    [HttpGet]
    public async Task<IActionResult> TrafficDashboard()
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetTrafficData(DateTime date)
    {
        if (!_authService.IsAdmin()) return Unauthorized();

        try
        {
            var summary = await _travelService.GetDailySummary(date);
            return Json(summary);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> RealTimeMap()
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");
        return View();
    }

    // ==================== STATISTIQUES ====================

    [HttpGet]
    public async Task<IActionResult> Statistics()
    {
        if (!_authService.IsAdmin()) return RedirectToAction("Login", "Account");

        var totalRequests = await _dbContext.TravelRequests.CountAsync();
        var pendingRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "Pending");
        var approvedRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "AdminApproved");
        var completedRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "Completed");
        var rejectedRequests = await _dbContext.TravelRequests.CountAsync(r => r.Status == "Rejected");

        var requestsByMonth = await _dbContext.TravelRequests
            .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
            .Select(g => new { Month = g.Key.Month, Year = g.Key.Year, Count = g.Count() })
            .OrderByDescending(g => g.Year)
            .ThenByDescending(g => g.Month)
            .Take(12)
            .ToListAsync();

        var popularDestinations = await _dbContext.TravelRequests
            .GroupBy(r => r.Destination)
            .Select(g => new { Destination = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(10)
            .ToListAsync();

        ViewBag.TotalRequests = totalRequests;
        ViewBag.PendingRequests = pendingRequests;
        ViewBag.ApprovedRequests = approvedRequests;
        ViewBag.CompletedRequests = completedRequests;
        ViewBag.RejectedRequests = rejectedRequests;
        ViewBag.RequestsByMonth = requestsByMonth;
        ViewBag.PopularDestinations = popularDestinations;

        return View();
    }

}