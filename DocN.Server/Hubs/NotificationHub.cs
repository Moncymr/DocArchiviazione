using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace DocN.Server.Hubs;

/// <summary>
/// SignalR Hub for real-time notifications
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// Adds the user to their personal group for targeted notifications
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            // Add user to their personal group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} connected to NotificationHub with connection {ConnectionId}", 
                userId, Context.ConnectionId);
        }
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            _logger.LogInformation("User {UserId} disconnected from NotificationHub", userId);
        }
        
        if (exception != null)
        {
            _logger.LogError(exception, "User disconnected with error");
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client calls this to mark a notification as read
    /// </summary>
    /// <param name="notificationId">The ID of the notification to mark as read</param>
    public async Task MarkAsRead(int notificationId)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} marking notification {NotificationId} as read", 
            userId, notificationId);
        
        // The actual database update is handled by the NotificationService
        // This is just for logging and potential real-time acknowledgment
        await Clients.Caller.SendAsync("NotificationMarkedAsRead", notificationId);
    }

    /// <summary>
    /// Client requests the current unread count
    /// </summary>
    public async Task GetUnreadCount()
    {
        // The actual count is fetched from the database by the controller
        // This method is here for consistency and future real-time updates
        await Task.CompletedTask;
    }

    /// <summary>
    /// Send a notification to a specific user
    /// This is called from server-side code, not by clients
    /// </summary>
    /// <param name="userId">Target user ID</param>
    /// <param name="notification">Notification object</param>
    public async Task SendNotificationToUser(string userId, object notification)
    {
        await Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", notification);
    }

    /// <summary>
    /// Broadcast a system-wide notification to all connected users
    /// This is called from server-side code, not by clients
    /// </summary>
    /// <param name="notification">Notification object</param>
    public async Task BroadcastNotification(object notification)
    {
        await Clients.All.SendAsync("ReceiveNotification", notification);
    }
}
