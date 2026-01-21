using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DocN.Data.Models;

namespace DocN.Data.Services;

public class LogService : ILogService
{
    private readonly IServiceProvider _serviceProvider;

    public LogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task LogInfoAsync(string category, string message, string? details = null, string? userId = null, string? fileName = null)
    {
        await LogAsync("Info", category, message, details, userId, fileName, null);
    }

    public async Task LogWarningAsync(string category, string message, string? details = null, string? userId = null, string? fileName = null)
    {
        await LogAsync("Warning", category, message, details, userId, fileName, null);
    }

    public async Task LogErrorAsync(string category, string message, string? details = null, string? userId = null, string? fileName = null, string? stackTrace = null)
    {
        await LogAsync("Error", category, message, details, userId, fileName, stackTrace);
    }

    public async Task LogDebugAsync(string category, string message, string? details = null, string? userId = null, string? fileName = null)
    {
        await LogAsync("Debug", category, message, details, userId, fileName, null);
    }

    private async Task LogAsync(string level, string category, string message, string? details, string? userId, string? fileName, string? stackTrace)
    {
        try
        {
            // Create a new scope and get a fresh DbContext for this operation to avoid tracking conflicts
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            if (context?.LogEntries == null)
            {
                Console.WriteLine($"[LOG SERVICE ERROR] Database context or LogEntries is null - Cannot log: [{level}] {category}: {message}");
                return;
            }

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message,
                Details = details,
                UserId = userId,
                FileName = fileName,
                StackTrace = stackTrace
            };

            context.LogEntries.Add(logEntry);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Fallback to console if database logging fails
            Console.WriteLine($"[LOG FAILURE] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [{level}] {category}: {message}");
            Console.WriteLine($"[LOG FAILURE] Original error: {ex.Message}");
        }
    }

    public async Task<List<LogEntry>> GetLogsAsync(string? category = null, string? userId = null, DateTime? fromDate = null, int maxRecords = 100)
    {
        try
        {
            // Create a new scope and get a fresh DbContext
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            if (context?.LogEntries == null)
            {
                Console.WriteLine("[LOG SERVICE ERROR] Database context or LogEntries is null");
                return new List<LogEntry>();
            }

            var query = context.LogEntries.AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(l => l.Category == category);
            }

            // Only filter by userId if it's not null or empty
            // This ensures we don't filter for empty string which would match nothing
            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(l => l.UserId == userId);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= fromDate.Value);
            }

            return await query
                .OrderByDescending(l => l.Timestamp)
                .Take(maxRecords)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOG SERVICE ERROR] Exception in GetLogsAsync: {ex.Message}\n{ex.StackTrace}");
            return new List<LogEntry>();
        }
    }

    public async Task<List<LogEntry>> GetUploadLogsAsync(string? userId = null, DateTime? fromDate = null, int maxRecords = 100)
    {
        try
        {
            // Create a new scope and get a fresh DbContext
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            if (context == null)
            {
                Console.WriteLine("[LOG SERVICE ERROR] Database context is null in GetUploadLogsAsync");
                return new List<LogEntry>();
            }
            
            if (context.LogEntries == null)
            {
                Console.WriteLine("[LOG SERVICE ERROR] LogEntries DbSet is null in GetUploadLogsAsync");
                return new List<LogEntry>();
            }

            var uploadCategories = new[] { "Upload", "Embedding", "AI", "Tag", "Metadata", "Category", "SimilaritySearch", "OCR", "UploadMultiple" };
            
            Console.WriteLine($"[LOG SERVICE] Building query - Categories: [{string.Join(", ", uploadCategories)}]");
            Console.WriteLine($"[LOG SERVICE] Filters - UserId: '{userId ?? "(ALL)"}', FromDate: {fromDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(ALL TIME)"}");
            
            var query = context.LogEntries
                .Where(l => uploadCategories.Contains(l.Category));

            // Only filter by userId if it's not null or empty
            // This ensures we don't filter for empty string which would match nothing
            if (!string.IsNullOrWhiteSpace(userId))
            {
                Console.WriteLine($"[LOG SERVICE] Applying userId filter: '{userId}'");
                query = query.Where(l => l.UserId == userId);
            }
            else
            {
                Console.WriteLine($"[LOG SERVICE] No userId filter - showing logs for all users");
            }

            if (fromDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= fromDate.Value);
            }

            var result = await query
                .OrderByDescending(l => l.Timestamp)
                .Take(maxRecords)
                .ToListAsync();
                
            Console.WriteLine($"[LOG SERVICE] Query executed - Found {result.Count} log entries");
            
            if (result.Count == 0)
            {
                // Debug: Check if there are ANY logs in the database
                var totalLogsCount = await context.LogEntries.CountAsync();
                Console.WriteLine($"[LOG SERVICE] Total logs in database: {totalLogsCount}");
                
                if (totalLogsCount > 0)
                {
                    // Check logs by category
                    foreach (var cat in uploadCategories)
                    {
                        var catCount = await context.LogEntries.Where(l => l.Category == cat).CountAsync();
                        if (catCount > 0)
                        {
                            Console.WriteLine($"[LOG SERVICE] Category '{cat}': {catCount} logs");
                        }
                    }
                    
                    // Check if there are logs for this user
                    if (!string.IsNullOrWhiteSpace(userId))
                    {
                        var userLogsCount = await context.LogEntries.Where(l => l.UserId == userId).CountAsync();
                        Console.WriteLine($"[LOG SERVICE] Logs for userId '{userId}': {userLogsCount}");
                    }
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOG SERVICE ERROR] Exception in GetUploadLogsAsync: {ex.Message}\n{ex.StackTrace}");
            return new List<LogEntry>();
        }
    }
}
