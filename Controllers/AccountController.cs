using Microsoft.AspNetCore.Mvc;
using GpsAdminServer.Services;
using System.Threading.Tasks;

namespace GpsAdminServer.Controllers;

public class AccountController : Controller
{
    private readonly AuthService _authService;

    public AccountController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (_authService.IsLoggedIn())
        {
            var user = _authService.GetCurrentUser();
            if (user?.Role == "Admin")
                return RedirectToAction("Dashboard", "Admin");
            return RedirectToAction("Dashboard", "Request");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {
        var user = await _authService.Login(username, password);
        if (user != null)
        {
            // DEBUG - Afficher dans la console du serveur
            System.Diagnostics.Debug.WriteLine($"=== LOGIN SUCCESS ===");
            System.Diagnostics.Debug.WriteLine($"Username: {user.Username}");
            System.Diagnostics.Debug.WriteLine($"Role from DB: {user.Role}");
            System.Diagnostics.Debug.WriteLine($"Redirecting to: {(user.Role == "Admin" ? "Admin" : "Request")}");

            if (user.Role == "Admin")
                return RedirectToAction("Dashboard", "Admin");
            return RedirectToAction("Dashboard", "Request");
        }

        System.Diagnostics.Debug.WriteLine($"=== LOGIN FAILED ===");
        System.Diagnostics.Debug.WriteLine($"Username: {username}");

        ViewBag.Error = "Nom d'utilisateur ou mot de passe incorrect";
        return View();
    }

    [HttpGet]
    public IActionResult Logout()
    {
        _authService.Logout();
        return RedirectToAction("Login");
    }

    [HttpPost]
    public IActionResult ClearSession()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}