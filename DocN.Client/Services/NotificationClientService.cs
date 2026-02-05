using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;
using DocN.Data.Models;
using System.Net.Http.Json;

namespace DocN.Client.Services;

public class NotificationClientService : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationClientService> _logger;
    private HubConnection? _hubConnection;
    private bool _isConnected;
    private string? _currentUserId;

    public event Action<Notification>? OnNotificationReceived;
    public event Action<int>? OnNotificationMarkedAsRead;
    public event Action? OnConnectionStateChanged;

    public bool IsConnected => _isConnected;
    public int UnreadCount { get; private set; }

    public NotificationClientService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationClientService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task StartAsync(string userId)
    {
        if (_isConnected || string.IsNullOrEmpty(userId))
        {
            return;
        }

        _currentUserId = userId;

        try
        {
            var backendUrl = _configuration["BackendApiUrl"] ?? "https://localhost:5211/";
            var hubUrl = $"{backendUrl.TrimEnd('/')}/hubs/notifications";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.UseDefaultCredentials = true;
                })
                .WithAutomaticReconnect(new[] 
                { 
                    TimeSpan.Zero, 
                    TimeSpan.FromSeconds(2), 
                    TimeSpan.FromSeconds(5), 
                    TimeSpan.FromSeconds(10) 
                })
                .Build();

            // Register handlers
            _hubConnection.On<object>("ReceiveNotification", async (notification) =>
            {
                _logger.LogInformation("Received notification from SignalR hub");
                
                // Deserialize to Notification object
                var json = System.Text.Json.JsonSerializer.Serialize(notification);
                var notif = System.Text.Json.JsonSerializer.Deserialize<Notification>(json);
                
                if (notif != null)
                {
                    UnreadCount++;
                    OnNotificationReceived?.Invoke(notif);
                    OnConnectionStateChanged?.Invoke();
                    
                    // Play sound if enabled (to be implemented in UI)
                    await PlayNotificationSoundAsync();
                }
            });

            _hubConnection.On<int>("NotificationMarkedAsRead", (notificationId) =>
            {
                _logger.LogInformation("Notification {Id} marked as read", notificationId);
                OnNotificationMarkedAsRead?.Invoke(notificationId);
            });

            _hubConnection.Reconnecting += error =>
            {
                _logger.LogWarning("SignalR connection lost. Reconnecting...");
                _isConnected = false;
                OnConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                _logger.LogInformation("SignalR reconnected with connection ID: {ConnectionId}", connectionId);
                _isConnected = true;
                OnConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };

            _hubConnection.Closed += error =>
            {
                _logger.LogError(error, "SignalR connection closed");
                _isConnected = false;
                OnConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };

            await _hubConnection.StartAsync();
            _isConnected = true;
            OnConnectionStateChanged?.Invoke();
            
            _logger.LogInformation("Connected to NotificationHub for user {UserId}", userId);
            
            // Load initial unread count
            await RefreshUnreadCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to NotificationHub");
            _isConnected = false;
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            _isConnected = false;
            OnConnectionStateChanged?.Invoke();
            _logger.LogInformation("Disconnected from NotificationHub");
        }
    }

    public async Task<List<Notification>> GetNotificationsAsync(bool? unreadOnly = null, int skip = 0, int take = 50)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var query = $"?skip={skip}&take={take}";
            if (unreadOnly.HasValue)
            {
                query += $"&unreadOnly={unreadOnly.Value}";
            }
            
            var response = await client.GetAsync($"api/notifications{query}");
            response.EnsureSuccessStatusCode();
            
            var notifications = await response.Content.ReadFromJsonAsync<List<Notification>>();
            return notifications ?? new List<Notification>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications");
            return new List<Notification>();
        }
    }

    public async Task<bool> MarkAsReadAsync(int notificationId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.PostAsync($"api/notifications/mark-read/{notificationId}", null);
            
            if (response.IsSuccessStatusCode)
            {
                UnreadCount = Math.Max(0, UnreadCount - 1);
                OnConnectionStateChanged?.Invoke();
                
                // Also send via SignalR if connected
                if (_hubConnection != null && _isConnected)
                {
                    await _hubConnection.SendAsync("MarkAsRead", notificationId);
                }
                
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
            return false;
        }
    }

    public async Task<bool> MarkAllAsReadAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.PostAsync("api/notifications/mark-all-read", null);
            
            if (response.IsSuccessStatusCode)
            {
                UnreadCount = 0;
                OnConnectionStateChanged?.Invoke();
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return false;
        }
    }

    public async Task<bool> DeleteNotificationAsync(int notificationId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.DeleteAsync($"api/notifications/{notificationId}");
            
            if (response.IsSuccessStatusCode)
            {
                await RefreshUnreadCountAsync();
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification");
            return false;
        }
    }

    public async Task RefreshUnreadCountAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.GetAsync("api/notifications/unread-count");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
                if (result != null)
                {
                    UnreadCount = result.Count;
                    OnConnectionStateChanged?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing unread count");
        }
    }

    public async Task<NotificationPreference> GetPreferencesAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.GetAsync("api/notifications/preferences");
            response.EnsureSuccessStatusCode();
            
            var preferences = await response.Content.ReadFromJsonAsync<NotificationPreference>();
            return preferences ?? new NotificationPreference();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notification preferences");
            return new NotificationPreference();
        }
    }

    public async Task<bool> UpdatePreferencesAsync(NotificationPreference preferences)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.PutAsJsonAsync("api/notifications/preferences", preferences);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification preferences");
            return false;
        }
    }

    private async Task PlayNotificationSoundAsync()
    {
        // This will be implemented in the UI layer using JavaScript Interop
        // For now, just a placeholder
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }

    private class UnreadCountResponse
    {
        public int Count { get; set; }
    }
}
