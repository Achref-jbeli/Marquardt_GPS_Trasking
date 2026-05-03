using Microsoft.AspNetCore.Mvc;
using GpsAdminServer.Services;
using GpsAdminServer.Models;
using Microsoft.EntityFrameworkCore;

namespace GpsAdminServer.Controllers;

public class HomeController : Controller
{
    private readonly FirebaseService _firebaseService;
    private readonly GpsDbContext _dbContext;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        FirebaseService firebaseService,
        GpsDbContext dbContext,
        ILogger<HomeController> logger)
    {
        _firebaseService = firebaseService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Dashboard()
    {
        try
        {
            ViewBag.LastUpdate = DateTime.Now.ToString("HH:mm:ss");
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du chargement du Dashboard");
            ViewBag.Error = ex.Message;
            return View();
        }
    }

    /// <summary>
    /// API qui retourne les positions brutes de Firebase
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPositions()
    {
        try
        {
            // Désactiver le cache
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            // Récupérer le JSON brut depuis Firebase
            var rawJson = await _firebaseService.GetRawJson();

            if (string.IsNullOrEmpty(rawJson) || rawJson == "null")
            {
                return Json(new { });
            }

            // Retourner directement le JSON brut
            return Content(rawJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur dans GetPositions");
            return Json(new { error = ex.Message });
        }
    }

    /// <summary>
    /// API de test - retourne les positions en texte brut
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TestPositions()
    {
        try
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";

            var rawJson = await _firebaseService.GetRawJson();

            return Content(rawJson, "application/json");
        }
        catch (Exception ex)
        {
            return Content($"Erreur: {ex.Message}", "text/plain");
        }
    }

    /// <summary>
    /// API qui retourne les données brutes de Firebase
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> RawFirebase()
    {
        try
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";

            var rawJson = await _firebaseService.GetRawJson();
            return Content(rawJson, "application/json");
        }
        catch (Exception ex)
        {
            return Content($"Erreur: {ex.Message}", "text/plain");
        }
    }

    /// <summary>
    /// API qui retourne la liste des chauffeurs depuis SQL Server
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetChauffeurs()
    {
        try
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";

            var chauffeurs = await _dbContext.Chauffeurs.ToListAsync();
            return Json(chauffeurs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur dans GetChauffeurs");
            return Json(new { error = ex.Message });
        }
    }

    /// <summary>
    /// API qui retourne l'historique des positions d'un chauffeur
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHistorique(string chauffeurId, int jours = 7)
    {
        try
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";

            var depuis = DateTime.Now.AddDays(-jours);
            var historique = await _dbContext.Positions
                .Where(p => p.ChauffeurId == chauffeurId && p.Timestamp >= depuis)
                .OrderBy(p => p.Timestamp)
                .ToListAsync();

            return Json(historique);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    /// <summary>
    /// API pour sauvegarder une position manuellement
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SauvegarderPosition([FromBody] Position position)
    {
        try
        {
            if (position == null)
                return BadRequest("Position invalide");

            position.Timestamp = DateTime.Now;
            await _dbContext.Positions.AddAsync(position);
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Position sauvegardée" });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}