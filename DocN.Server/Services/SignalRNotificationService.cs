using Microsoft.AspNetCore.SignalR;
using DocN.Data.Services;
using DocN.Data.Models;
using DocN.Server.Hubs;

namespace DocN.Server.Services;

/// <summary>
/// Wrapper service that handles SignalR notifications for the NotificationService
/// IMPORTANT: Uses concrete NotificationService class (not interface) to avoid circular dependency
/// </summary>
public class SignalRNotificationService : INotificationService
{
    private readonly NotificationService _notificationService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        NotificationService notificationService,
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationService> logger)
    {
        _notificationService = notificationService;
        _hubContext = hubContext;
        _logger = logger;
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
        // Create notification using the base service
        var notification = await _notificationService.CreateNotificationAsync(
            userId, type, title, message, link, icon, isImportant);

        // Send real-time notification via SignalR
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
            _logger.LogWarning(ex, "Failed to send real-time notification via SignalR for user {UserId}", userId);
            // Don't fail the whole operation if SignalR fails
        }

        return notification;
    }

    // Delegate all other methods to the base service
    public Task<List<Notification>> GetNotificationsAsync(string userId, bool? unreadOnly = null, int skip = 0, int take = 50)
        => _notificationService.GetNotificationsAsync(userId, unreadOnly, skip, take);

    public Task<int> GetUnreadCountAsync(string userId)
        => _notificationService.GetUnreadCountAsync(userId);

    public Task<bool> MarkAsReadAsync(int notificationId, string userId)
        => _notificationService.MarkAsReadAsync(notificationId, userId);

    public Task<bool> MarkAllAsReadAsync(string userId)
        => _notificationService.MarkAllAsReadAsync(userId);

    public Task<bool> DeleteNotificationAsync(int notificationId, string userId)
        => _notificationService.DeleteNotificationAsync(notificationId, userId);

    public Task CleanupOldNotificationsAsync(int daysToKeep = 30)
        => _notificationService.CleanupOldNotificationsAsync(daysToKeep);

    public Task<NotificationPreference> GetOrCreatePreferenceAsync(string userId)
        => _notificationService.GetOrCreatePreferenceAsync(userId);

    public Task<NotificationPreference> UpdatePreferenceAsync(NotificationPreference preference)
        => _notificationService.UpdatePreferenceAsync(preference);
}
