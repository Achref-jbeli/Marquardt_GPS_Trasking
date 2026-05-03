using GpsAdminServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace GpsAdminServer.Services;

public class TravelService
{
    private readonly GpsDbContext _dbContext;
    private readonly ILogger<TravelService> _logger;

    public TravelService(GpsDbContext dbContext, ILogger<TravelService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DailySummary> GetDailySummary(DateTime date)
    {
        var startDate = date.Date;
        var endDate = startDate.AddDays(1);

        var positions = await _dbContext.Positions
            .Where(p => p.Timestamp >= startDate && p.Timestamp < endDate)
            .OrderBy(p => p.ChauffeurId)
            .ThenBy(p => p.Timestamp)
            .ToListAsync();

        var travels = new List<TravelSummary>();

        // Grouper par chauffeur
        var chauffeurGroups = positions.GroupBy(p => p.ChauffeurId);

        foreach (var group in chauffeurGroups)
        {
            var chauffeurPositions = group.ToList();
            if (chauffeurPositions.Count < 2) continue;

            var travel = new TravelSummary
            {
                ChauffeurId = group.Key,
                Nom = chauffeurPositions.First().ChauffeurId,
                TotalPoints = chauffeurPositions.Count,
                StartTime = chauffeurPositions.First().Timestamp,
                EndTime = chauffeurPositions.Last().Timestamp,
                TotalTime = chauffeurPositions.Last().Timestamp - chauffeurPositions.First().Timestamp,
                Trajectory = chauffeurPositions
            };

            // Calculer la distance et les vitesses
            double travelDistance = 0;
            double maxSpeed = 0;
            double speedSum = 0;

            for (int i = 1; i < chauffeurPositions.Count; i++)
            {
                var prev = chauffeurPositions[i - 1];
                var curr = chauffeurPositions[i];

                // Distance entre deux points (formule de Haversine)
                var distance = CalculateDistance(
                    prev.Latitude, prev.Longitude,
                    curr.Latitude, curr.Longitude);
                travelDistance += distance;

                // Vitesse moyenne entre deux points
                if (curr.SpeedKmh > 0)
                {
                    speedSum += curr.SpeedKmh;
                    if (curr.SpeedKmh > maxSpeed) maxSpeed = curr.SpeedKmh;
                }
            }

            travel.DistanceKm = Math.Round(travelDistance, 2);
            travel.MaxSpeed = Math.Round(maxSpeed, 2);
            travel.AvgSpeed = travel.TotalPoints > 1
                ? Math.Round(speedSum / (travel.TotalPoints - 1), 2)
                : 0;

            travels.Add(travel);
        }

        double totalDistanceAll = travels.Sum(t => t.DistanceKm);
        int totalTravels = travels.Count;
        int activeChauffeurs = travels.Count(t => t.DistanceKm > 0.1);

        return new DailySummary
        {
            Date = date,
            TotalTravels = totalTravels,
            ActiveChauffeurs = activeChauffeurs,
            TotalDistanceKm = Math.Round(totalDistanceAll, 2),
            AvgSpeedAll = totalTravels > 0 ? Math.Round(travels.Average(t => t.AvgSpeed), 2) : 0,
            Travels = travels
        };
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 6371; // Rayon de la Terre en km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees) => degrees * Math.PI / 180;
}