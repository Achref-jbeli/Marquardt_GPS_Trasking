using Microsoft.EntityFrameworkCore;

namespace GpsAdminServer.Models;

public class GpsDbContext : DbContext
{
    public GpsDbContext(DbContextOptions<GpsDbContext> options) : base(options) { }

    // ========== TABLES GPS ==========
    public DbSet<Chauffeur> Chauffeurs { get; set; }
    public DbSet<Position> Positions { get; set; }
    public DbSet<Mission> Missions { get; set; }

    // ========== TABLES GESTION DES UTILISATEURS ==========
    public DbSet<ApplicationUser> Users { get; set; }
    public DbSet<TravelRequest> TravelRequests { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Index pour les performances
        modelBuilder.Entity<Chauffeur>()
            .HasIndex(c => c.ChauffeurId)
            .IsUnique();

        modelBuilder.Entity<Position>()
            .HasIndex(p => p.Timestamp);

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<TravelRequest>()
            .HasIndex(r => r.Status);

        modelBuilder.Entity<TravelRequest>()
            .HasIndex(r => r.TravelDate);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.UserId);
    }
}

// ========== MODÈLE CHAUFFEUR ==========
public class Chauffeur
{
    public int Id { get; set; }
    public string ChauffeurId { get; set; } = "";
    public string Nom { get; set; } = "";
    public string MotDePasse { get; set; } = "";
    public string? NumeroSerie { get; set; }
    public string? Marque { get; set; }
    public string? Modele { get; set; }
    public string? Annee { get; set; }
    public string Status { get; set; } = "hors_ligne";
    public DateTime? LastLogin { get; set; }
    public DateTime? TrackingStarted { get; set; }
    public DateTime? TrackingStopped { get; set; }
    public DateTime? LastLogout { get; set; }
    public DateTime CreeLe { get; set; } = DateTime.Now;
}

// ========== MODÈLE POSITION ==========
public class Position
{
    public int Id { get; set; }
    public string ChauffeurId { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double SpeedKmh { get; set; }
    public double AltitudeM { get; set; }
    public double AccuracyM { get; set; }
    public DateTime Timestamp { get; set; }
    public string? MapsLink { get; set; }
}

// ========== MODÈLE MISSION ==========
public class Mission
{
    public int Id { get; set; }
    public string ChauffeurId { get; set; } = "";
    public string MissionId { get; set; } = "";
    public string Client { get; set; } = "";
    public string Adresse { get; set; } = "";
    public string Heure { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime Date { get; set; }
    public DateTime CreeLe { get; set; } = DateTime.Now;
}

// ========== MODÈLE UTILISATEUR ==========
public class ApplicationUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "User"; // Admin, Supervisor, User
    public int? SupervisorId { get; set; }
    public string Department { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ========== MODÈLE DEMANDE DE VOYAGE ==========
public class TravelRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string Departure { get; set; } = "";
    public string Destination { get; set; } = "";
    public DateTime TravelDate { get; set; }
    public DateTime ArrivalTime { get; set; }
    public string Purpose { get; set; } = "";
    public string Status { get; set; } = "Pending"; // Pending, SupervisorApproved, AdminApproved, Rejected, Completed, Cancelled
    public int? SupervisorId { get; set; }
    public int? AdminId { get; set; }
    public int? VehicleId { get; set; }
    public DateTime? SupervisorApprovedAt { get; set; }
    public DateTime? AdminApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? SupervisorComments { get; set; }
    public string? AdminComments { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ========== MODÈLE VÉHICULE ==========
public class Vehicle
{
    public int Id { get; set; }
    public string PlateNumber { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string Year { get; set; } = "";
    public string Status { get; set; } = "Available"; // Available, InUse, Maintenance
    public string? ChauffeurId { get; set; }
    public int? CurrentRequestId { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ========== MODÈLE NOTIFICATION ==========
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Type { get; set; } = ""; // Request, Approval, Rejection, Info
    public int? RequestId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}