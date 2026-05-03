using GpsAdminServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace GpsAdminServer.Services;

public class AuthService
{
    private readonly GpsDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(GpsDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ApplicationUser?> Login(string username, string password)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.Password == password && u.IsActive);

        if (user != null)
        {
            _httpContextAccessor.HttpContext?.Session.SetInt32("UserId", user.Id);
            _httpContextAccessor.HttpContext?.Session.SetString("UserRole", user.Role);
            _httpContextAccessor.HttpContext?.Session.SetString("UserName", user.FullName);
        }

        return user;
    }

    public void Logout()
    {
        _httpContextAccessor.HttpContext?.Session.Clear();
    }

    public ApplicationUser? GetCurrentUser()
    {
        var userId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");
        if (userId.HasValue)
        {
            return _dbContext.Users.FirstOrDefault(u => u.Id == userId.Value);
        }
        return null;
    }

    public bool IsLoggedIn()
    {
        return _httpContextAccessor.HttpContext?.Session.GetInt32("UserId").HasValue ?? false;
    }

    public bool IsAdmin()
    {
        var role = _httpContextAccessor.HttpContext?.Session.GetString("UserRole");
        return role == "Admin";
    }

    public bool IsSupervisor()
    {
        var role = _httpContextAccessor.HttpContext?.Session.GetString("UserRole");
        return role == "Supervisor" || role == "Admin";
    }
}