using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace GpsAdminServer.Services;

public class FirebaseService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FirebaseService> _logger;

    public FirebaseService(IConfiguration configuration, ILogger<FirebaseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Récupère les données brutes JSON de Firebase
    /// </summary>
    public async Task<string> GetRawJson()
    {
        var dbUrl = _configuration["Firebase:DatabaseUrl"];
        var url = $"{dbUrl}/positions_actuelles.json";

        try
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur GetRawJson");
            return $"Erreur: {ex.Message}";
        }
    }

    /// <summary>
    /// Vérifie la connexion à Firebase
    /// </summary>
    public async Task<bool> CheckConnection()
    {
        try
        {
            var dbUrl = _configuration["Firebase:DatabaseUrl"];
            var url = $"{dbUrl}/positions_actuelles.json?shallow=true";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Récupère toutes les positions actuelles
    /// </summary>
    public async Task<Dictionary<string, JObject>> GetCurrentPositions()
    {
        var dbUrl = _configuration["Firebase:DatabaseUrl"];
        var url = $"{dbUrl}/positions_actuelles.json";

        try
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!string.IsNullOrEmpty(json) && json != "null")
            {
                var data = JObject.Parse(json);
                return data.ToObject<Dictionary<string, JObject>>() ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur GetCurrentPositions");
        }

        return new Dictionary<string, JObject>();
    }

    /// <summary>
    /// Récupère la position d'un chauffeur spécifique
    /// </summary>
    public async Task<JObject?> GetChauffeurPosition(string chauffeurId)
    {
        var dbUrl = _configuration["Firebase:DatabaseUrl"];
        var url = $"{dbUrl}/positions_actuelles/{chauffeurId}.json";

        try
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!string.IsNullOrEmpty(json) && json != "null")
            {
                return JObject.Parse(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erreur GetChauffeurPosition {chauffeurId}");
        }

        return null;
    }

    /// <summary>
    /// Récupère la liste de tous les chauffeurs
    /// </summary>
    public async Task<Dictionary<string, JObject>> GetChauffeurs()
    {
        var dbUrl = _configuration["Firebase:DatabaseUrl"];
        var url = $"{dbUrl}/chauffeurs.json";

        try
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!string.IsNullOrEmpty(json) && json != "null")
            {
                var data = JObject.Parse(json);
                return data.ToObject<Dictionary<string, JObject>>() ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur GetChauffeurs");
        }

        return new Dictionary<string, JObject>();
    }

    /// <summary>
    /// Récupère les positions avec informations chauffeur
    /// </summary>
    public async Task<List<object>> GetChauffeursWithPositions()
    {
        var result = new List<object>();

        try
        {
            var positions = await GetCurrentPositions();

            foreach (var pos in positions)
            {
                result.Add(new
                {
                    ChauffeurId = pos.Key,
                    Latitude = pos.Value["lat"]?.ToObject<double>() ?? 0,
                    Longitude = pos.Value["lng"]?.ToObject<double>() ?? 0,
                    SpeedKmh = pos.Value["speed_kmh"]?.ToObject<double>() ?? 0,
                    Nom = pos.Value["nom"]?.ToString() ?? pos.Key,
                    LastUpdate = pos.Value["last_update"]?.ToString() ?? "",
                    MapsLink = pos.Value["maps_link"]?.ToString() ?? ""
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur GetChauffeursWithPositions");
        }

        return result;
    }

    /// <summary>
    /// Récupère l'historique d'un chauffeur
    /// </summary>
    public async Task<List<JObject>> GetChauffeurHistory(string chauffeurId, int limit = 100)
    {
        var dbUrl = _configuration["Firebase:DatabaseUrl"];
        var url = $"{dbUrl}/chauffeurs/{chauffeurId}/positions.json?orderBy=\"$key\"&limitToLast={limit}";

        var result = new List<JObject>();

        try
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!string.IsNullOrEmpty(json) && json != "null")
            {
                var data = JObject.Parse(json);
                foreach (var item in data.Properties())
                {
                    if (item.Value is JObject obj)
                    {
                        result.Add(obj);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erreur GetChauffeurHistory {chauffeurId}");
        }

        return result;
    }

    /// <summary>
    /// Met à jour le statut d'un chauffeur
    /// </summary>
    public async Task<bool> UpdateChauffeurStatus(string chauffeurId, string status)
    {
        var dbUrl = _configuration["Firebase:DatabaseUrl"];
        var url = $"{dbUrl}/chauffeurs/{chauffeurId}/status.json";

        try
        {
            var content = new StringContent($"\"{status}\"", System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erreur UpdateChauffeurStatus {chauffeurId}");
            return false;
        }
    }
}