using Microsoft.EntityFrameworkCore;
using DocN.Data.Models;

namespace DocN.Data.Services;

/// <summary>
/// Service for tracking user activities
/// </summary>
public class UserActivityService : IUserActivityService
{
    private readonly ApplicationDbContext _context;

    public UserActivityService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserActivity>> GetUserActivitiesAsync(string userId, int count = 20)
    {
        return await _context.UserActivities
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .Include(a => a.Document)
            .ToListAsync();
    }

    public async Task RecordActivityAsync(string userId, string activityType, string description, int? documentId = null, string? metadata = null)
    {
        var activity = new UserActivity
        {
            UserId = userId,
            ActivityType = activityType,
            Description = description,
            DocumentId = documentId,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        };

        _context.UserActivities.Add(activity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<UserActivity>> GetRecentDocumentActivitiesAsync(string userId, int count = 10)
    {
        return await _context.UserActivities
            .Where(a => a.UserId == userId && a.DocumentId != null)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .Include(a => a.Document)
            .ToListAsync();
    }
}
