using GpsAdminServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace GpsAdminServer.Services;

public class NotificationService
{
    private readonly GpsDbContext _dbContext;

    public NotificationService(GpsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SendNotification(int userId, string title, string message, string type, int? requestId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RequestId = requestId,
            CreatedAt = DateTime.Now,
            IsRead = false
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<Notification>> GetUnreadNotifications(int userId)
    {
        return await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .ToListAsync();
    }

    public async Task MarkAsRead(int notificationId, int userId)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification != null)
        {
            notification.IsRead = true;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsRead(int userId)
    {
        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in notifications)
        {
            n.IsRead = true;
        }
        await _dbContext.SaveChangesAsync();
    }
}