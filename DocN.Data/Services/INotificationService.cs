using DocN.Data.Models;

namespace DocN.Data.Services;

public interface INotificationService
{
    Task<Notification> CreateNotificationAsync(string userId, string type, string title, string message, 
        string? link = null, string icon = "info", bool isImportant = false);
    Task<List<Notification>> GetNotificationsAsync(string userId, bool? unreadOnly = null, int skip = 0, int take = 50);
    Task<int> GetUnreadCountAsync(string userId);
    Task<bool> MarkAsReadAsync(int notificationId, string userId);
    Task<bool> MarkAllAsReadAsync(string userId);
    Task<bool> DeleteNotificationAsync(int notificationId, string userId);
    Task CleanupOldNotificationsAsync(int daysToKeep = 30);
    
    // Preference methods
    Task<NotificationPreference> GetOrCreatePreferenceAsync(string userId);
    Task<NotificationPreference> UpdatePreferenceAsync(NotificationPreference preference);
}
