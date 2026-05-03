using Microsoft.AspNetCore.Mvc;
using GpsAdminServer.Services;
using GpsAdminServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;

namespace GpsAdminServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApiController : ControllerBase
{
    private readonly FirebaseService _firebaseService;
    private readonly GpsDbContext _dbContext;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        FirebaseService firebaseService,
        GpsDbContext dbContext,
        ILogger<ApiController> logger)
    {
        _firebaseService = firebaseService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new { status = "OK", timestamp = DateTime.Now });
    }

    [HttpGet("check-firebase")]
    public async Task<IActionResult> CheckFirebase()
    {
        var isConnected = await _firebaseService.CheckConnection();
        return Ok(new { connected = isConnected });
    }

    [HttpGet("positions")]
    public async Task<IActionResult> GetCurrentPositions()
    {
        try
        {
            var positions = await _firebaseService.GetCurrentPositions();
            return Ok(positions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("raw")]
    public async Task<IActionResult> GetRawFirebase()
    {
        try
        {
            var raw = await _firebaseService.GetRawJson();
            return Content(raw, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("position/{chauffeurId}")]
    public async Task<IActionResult> GetChauffeurPosition(string chauffeurId)
    {
        try
        {
            var position = await _firebaseService.GetChauffeurPosition(chauffeurId);
            if (position == null) return NotFound();
            return Ok(position);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("chauffeurs")]
    public async Task<IActionResult> GetChauffeurs()
    {
        try
        {
            var chauffeurs = await _firebaseService.GetChauffeurs();
            return Ok(chauffeurs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("chauffeurs/sql")]
    public async Task<IActionResult> GetChauffeursFromSql()
    {
        try
        {
            var chauffeurs = await _dbContext.Chauffeurs.ToListAsync();
            return Ok(chauffeurs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("positions/detail")]
    public async Task<IActionResult> GetDetailedPositions()
    {
        try
        {
            var result = await _firebaseService.GetChauffeursWithPositions();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("historique/{chauffeurId}")]
    public async Task<IActionResult> GetHistorique(string chauffeurId, int jours = 7, int limit = 100)
    {
        try
        {
            var depuis = DateTime.Now.AddDays(-jours);
            var historique = await _dbContext.Positions
                .Where(p => p.ChauffeurId == chauffeurId && p.Timestamp >= depuis)
                .OrderByDescending(p => p.Timestamp)
                .Take(limit)
                .ToListAsync();
            return Ok(historique);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("historique/firebase/{chauffeurId}")]
    public async Task<IActionResult> GetFirebaseHistorique(string chauffeurId, int limit = 50)
    {
        try
        {
            var historique = await _firebaseService.GetChauffeurHistory(chauffeurId, limit);
            return Ok(historique);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var positions = await _firebaseService.GetCurrentPositions();
            var chauffeurs = await _dbContext.Chauffeurs.CountAsync();
            var historiqueCount = await _dbContext.Positions.CountAsync();

            int actifs = 0;
            foreach (var pos in positions)
            {
                // Utiliser ToObject<double>() au lieu de Value<double>()
                var lat = pos.Value["lat"]?.ToObject<double>();
                var lng = pos.Value["lng"]?.ToObject<double>();

                if (lat.HasValue && lng.HasValue && lat.Value != 0 && lng.Value != 0)
                {
                    actifs++;
                }
            }

            return Ok(new
            {
                totalChauffeurs = chauffeurs,
                actifsEnLigne = actifs,
                totalPositionsHistorique = historiqueCount,
                lastUpdate = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur GetStats");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPut("chauffeurs/{chauffeurId}/status")]
    public async Task<IActionResult> UpdateStatus(string chauffeurId, [FromBody] string status)
    {
        try
        {
            var result = await _firebaseService.UpdateChauffeurStatus(chauffeurId, status);
            return result ? Ok(new { success = true }) : BadRequest();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("chauffeurs")]
    public async Task<IActionResult> AddChauffeur([FromBody] Chauffeur chauffeur)
    {
        try
        {
            if (string.IsNullOrEmpty(chauffeur.ChauffeurId))
                return BadRequest(new { error = "ChauffeurId requis" });

            var exists = await _dbContext.Chauffeurs
                .AnyAsync(c => c.ChauffeurId == chauffeur.ChauffeurId);

            if (exists)
                return Conflict(new { error = "Déjà existant" });

            chauffeur.CreeLe = DateTime.Now;
            await _dbContext.Chauffeurs.AddAsync(chauffeur);
            await _dbContext.SaveChangesAsync();

            return Ok(chauffeur);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}