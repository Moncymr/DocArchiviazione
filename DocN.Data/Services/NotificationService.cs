using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DocN.Data.Models;
using Microsoft.AspNetCore.SignalR;

namespace DocN.Data.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly IHubContext<DocN.Server.Hubs.NotificationHub>? _hubContext;

    public NotificationService(
        ApplicationDbContext context,
        ILogger<NotificationService> logger,
        IHubContext<DocN.Server.Hubs.NotificationHub>? hubContext = null)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<Notification> CreateNotificationAsync(
        string userId,
        string type,
        string title,
        string message,
        string? link = null,
        string icon = "info",
        bool isImportant = false)
    {
        try
        {
            // Check user preferences
            var preferences = await GetOrCreatePreferenceAsync(userId);
            
            // Check if this notification type is enabled for the user
            bool isEnabled = type switch
            {
                NotificationTypes.DocumentProcessed => preferences.EnableDocumentProcessed,
                NotificationTypes.NewComment => preferences.EnableComments,
                NotificationTypes.Mention => preferences.EnableMentions,
                NotificationTypes.SystemAlert => preferences.EnableSystemAlerts,
                NotificationTypes.TaskCompleted => preferences.EnableTaskCompleted,
                _ => true
            };

            if (!isEnabled)
            {
                _logger.LogInformation("Notification type {Type} is disabled for user {UserId}", type, userId);
                // Return a dummy notification object without saving to database
                return new Notification
                {
                    UserId = userId,
                    Type = type,
                    Title = title,
                    Message = message,
                    Icon = icon,
                    IsRead = true // Mark as read since it wasn't sent
                };
            }

            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                Link = link,
                Icon = icon,
                IsImportant = isImportant,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created notification {Id} for user {UserId}: {Title}", 
                notification.Id, userId, title);

            // Send real-time notification via SignalR if hub context is available
            if (_hubContext != null)
            {
                try
                {
                    await _hubContext.Clients.Group($"user_{userId}")
                        .SendAsync("ReceiveNotification", new
                        {
                            notification.Id,
                            notification.Type,
                            notification.Title,
                            notification.Message,
                            notification.Link,
                            notification.Icon,
                            notification.IsImportant,
                            notification.CreatedAt
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send real-time notification via SignalR");
                    // Don't fail the whole operation if SignalR fails
                }
            }

            return notification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<Notification>> GetNotificationsAsync(
        string userId,
        bool? unreadOnly = null,
        int skip = 0,
        int take = 50)
    {
        try
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId);

            if (unreadOnly.HasValue && unreadOnly.Value)
            {
                query = query.Where(n => !n.IsRead);
            }

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return notifications;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        try
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                _logger.LogWarning("Notification {Id} not found for user {UserId}", notificationId, userId);
                return false;
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Marked notification {Id} as read for user {UserId}", notificationId, userId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {Id} as read", notificationId);
            throw;
        }
    }

    public async Task<bool> MarkAllAsReadAsync(string userId)
    {
        try
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                var now = DateTime.UtcNow;
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = now;
                }

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", 
                    unreadNotifications.Count, userId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> DeleteNotificationAsync(int notificationId, string userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return false;
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted notification {Id} for user {UserId}", notificationId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {Id}", notificationId);
            throw;
        }
    }

    public async Task CleanupOldNotificationsAsync(int daysToKeep = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var oldNotifications = await _context.Notifications
                .Where(n => n.CreatedAt < cutoffDate)
                .ToListAsync();

            if (oldNotifications.Any())
            {
                _context.Notifications.RemoveRange(oldNotifications);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Cleaned up {Count} old notifications", oldNotifications.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old notifications");
            throw;
        }
    }

    public async Task<NotificationPreference> GetOrCreatePreferenceAsync(string userId)
    {
        try
        {
            var preference = await _context.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (preference == null)
            {
                preference = new NotificationPreference
                {
                    UserId = userId,
                    EnableDocumentProcessed = true,
                    EnableComments = true,
                    EnableMentions = true,
                    EnableSystemAlerts = true,
                    EnableTaskCompleted = true,
                    EnableSound = true,
                    EnableDesktopNotifications = false,
                    EmailDigestFrequency = "none",
                    CreatedAt = DateTime.UtcNow
                };

                _context.NotificationPreferences.Add(preference);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Created default notification preferences for user {UserId}", userId);
            }

            return preference;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting/creating notification preferences for user {UserId}", userId);
            throw;
        }
    }

    public async Task<NotificationPreference> UpdatePreferenceAsync(NotificationPreference preference)
    {
        try
        {
            preference.UpdatedAt = DateTime.UtcNow;
            _context.NotificationPreferences.Update(preference);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated notification preferences for user {UserId}", preference.UserId);
            return preference;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification preferences");
            throw;
        }
    }
}
