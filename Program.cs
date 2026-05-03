using GpsAdminServer.Models;
using GpsAdminServer.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ========== AJOUTER LES SERVICES ==========

// Ajouter les contrôleurs MVC
builder.Services.AddControllersWithViews();

// Configuration SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<GpsDbContext>(options =>
    options.UseSqlServer(connectionString));

// Ajouter FirebaseService (injection de dépendances)
builder.Services.AddScoped<FirebaseService>();

// ========== SERVICES POUR L'AUTHENTIFICATION ==========
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ========== SERVICES PERSONNALISÉS ==========
builder.Services.AddScoped<AuthService>();           // ← AJOUTÉ
builder.Services.AddScoped<NotificationService>();   // ← AJOUTÉ
builder.Services.AddScoped<TravelService>();

// Ajouter le service de sauvegarde automatique (background service)
builder.Services.AddHostedService<PositionSaverService>();

// Ajouter HttpClient pour les appels API
builder.Services.AddHttpClient();

// Configuration CORS (pour autoriser les requêtes depuis d'autres domaines)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

var app = builder.Build();

// ========== CONFIGURATION DU PIPELINE HTTP ==========

// Activer Session (doit être avant UseAuthorization)
app.UseSession();

// Utiliser CORS
app.UseCors("AllowAll");

// Configurer le pipeline pour l'environnement de développement
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// ========== CONFIGURATION DES ROUTES ==========

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Routes API simplifiées
app.MapGet("/api/positions", async (FirebaseService firebaseService) =>
{
    var positions = await firebaseService.GetCurrentPositions();
    return Results.Ok(positions);
});

app.MapGet("/api/chauffeurs", async (FirebaseService firebaseService) =>
{
    var chauffeurs = await firebaseService.GetChauffeurs();
    return Results.Ok(chauffeurs);
});

app.MapGet("/api/raw", async (FirebaseService firebaseService) =>
{
    var raw = await firebaseService.GetRawJson();
    return Results.Content(raw, "application/json");
});

// ========== INITIALISATION DE LA BASE DE DONNÉES ==========

using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<GpsDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ Base de données SQL Server vérifiée/créée avec succès");

        // Compter les chauffeurs existants
        var chauffeursCount = await dbContext.Chauffeurs.CountAsync();
        Console.WriteLine($"📊 Nombre de chauffeurs en base: {chauffeursCount}");

        // Compter les utilisateurs
        var usersCount = await dbContext.Users.CountAsync();
        Console.WriteLine($"👥 Nombre d'utilisateurs en base: {usersCount}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erreur lors de la connexion à SQL Server: {ex.Message}");
        Console.WriteLine("   Vérifiez que SQL Server est en cours d'exécution");
    }
}

// ========== AFFICHER LES INFORMATIONS DE DÉMARRAGE ==========

Console.WriteLine("");
Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║              GPS ADMIN SERVER - DÉMARRÉ                   ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  🌐 URLs:                                                ║");
Console.WriteLine($"║     http://localhost:5126                               ║");
Console.WriteLine($"╠══════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  🔐 Login: /Account/Login                               ║");
Console.WriteLine($"║  🗺️  Carte GPS: /Home/Dashboard                         ║");
Console.WriteLine($"║  👑 Admin: /Admin/Dashboard                             ║");
Console.WriteLine($"║  👤 Employé: /Request/Dashboard                         ║");
Console.WriteLine($"╠══════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  📡 API: /api/positions, /api/chauffeurs                ║");
Console.WriteLine($"╚══════════════════════════════════════════════════════════╝");
Console.WriteLine("");
Console.WriteLine("🔄 Sauvegarde automatique des positions active toutes les 5 secondes");
Console.WriteLine("Appuyez sur Ctrl+C pour arrêter le serveur.");

app.Run();