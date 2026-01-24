using DocN.Data.Models;

namespace DocN.Data.Services;

/// <summary>
/// Service interface for tracking user activities
/// </summary>
public interface IUserActivityService
{
    Task<List<UserActivity>> GetUserActivitiesAsync(string userId, int count = 20);
    Task RecordActivityAsync(string userId, string activityType, string description, int? documentId = null, string? metadata = null);
    Task<List<UserActivity>> GetRecentDocumentActivitiesAsync(string userId, int count = 10);
}
