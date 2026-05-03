using System.Collections.Generic;
using System;

namespace GpsAdminServer.Models;

public class TravelSummary
{
    public DateTime Date { get; set; }
    public string ChauffeurId { get; set; } = "";
    public string Nom { get; set; } = "";
    public int TotalPoints { get; set; }
    public double DistanceKm { get; set; }
    public double AvgSpeed { get; set; }
    public double MaxSpeed { get; set; }
    public TimeSpan TotalTime { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<Position> Trajectory { get; set; } = new();
}

public class DailySummary
{
    public DateTime Date { get; set; }
    public int TotalTravels { get; set; }
    public int ActiveChauffeurs { get; set; }
    public double TotalDistanceKm { get; set; }
    public double AvgSpeedAll { get; set; }
    public List<TravelSummary> Travels { get; set; } = new();
}