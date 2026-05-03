using GpsAdminServer.Models;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace GpsAdminServer.Services;

public class PositionSaverService : BackgroundService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<PositionSaverService> _logger;
	private readonly HttpClient _httpClient;
	private readonly IConfiguration _configuration;
	private string? _lastJson;

	public PositionSaverService(
		IServiceProvider serviceProvider,
		ILogger<PositionSaverService> logger,
		IConfiguration configuration)
	{
		_serviceProvider = serviceProvider;
		_logger = logger;
		_configuration = configuration;
		_httpClient = new HttpClient();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("📦 PositionSaverService démarré - Sauvegarde SQL Server active");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var dbUrl = _configuration["Firebase:DatabaseUrl"];
				var url = $"{dbUrl}/positions_actuelles.json";

				var response = await _httpClient.GetAsync(url, stoppingToken);
				var currentJson = await response.Content.ReadAsStringAsync(stoppingToken);

				if (currentJson != _lastJson && !string.IsNullOrEmpty(currentJson) && currentJson != "null")
				{
					_logger.LogInformation("🔄 Nouvelles positions détectées, sauvegarde dans SQL Server...");
					await SavePositionsToDatabase(currentJson);
					_lastJson = currentJson;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "❌ Erreur dans PositionSaverService");
			}

			await Task.Delay(5000, stoppingToken);
		}
	}

	private async Task SavePositionsToDatabase(string json)
	{
		try
		{
			using var scope = _serviceProvider.CreateScope();
			var dbContext = scope.ServiceProvider.GetRequiredService<GpsDbContext>();

			var data = JObject.Parse(json);
			int savedCount = 0;

			foreach (var item in data.Properties())
			{
				var chauffeurId = item.Name;
				var position = item.Value as JObject;

				if (position != null)
				{
					var lat = position["lat"]?.ToObject<double>() ?? 0;
					var lng = position["lng"]?.ToObject<double>() ?? 0;
					var speed = position["speed_kmh"]?.ToObject<double>() ?? 0;
					var altitude = position["altitude_m"]?.ToObject<double>() ?? 0;
					var accuracy = position["accuracy_m"]?.ToObject<double>() ?? 0;
					var mapsLink = position["maps_link"]?.ToString() ?? "";

					DateTime parsedTimestamp;
					if (!DateTime.TryParse(position["last_update"]?.ToString(), out parsedTimestamp))
					{
						parsedTimestamp = DateTime.Now;
					}

					if (lat != 0 && lng != 0)
					{
						var newPosition = new Position
						{
							ChauffeurId = chauffeurId,
							Latitude = lat,
							Longitude = lng,
							SpeedKmh = speed,
							AltitudeM = altitude,
							AccuracyM = accuracy,
							Timestamp = parsedTimestamp,
							MapsLink = mapsLink
						};

						await dbContext.Positions.AddAsync(newPosition);
						savedCount++;
					}
				}
			}

			if (savedCount > 0)
			{
				await dbContext.SaveChangesAsync();
				_logger.LogInformation($"✅ {savedCount} positions sauvegardées dans SQL Server");
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "❌ Erreur lors de la sauvegarde SQL Server");
		}
	}
}