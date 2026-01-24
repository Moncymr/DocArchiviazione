using Microsoft.EntityFrameworkCore;
using DocN.Data.Models;
using DocN.Data.Constants;

namespace DocN.Data.Services;

/// <summary>
/// Service for managing dashboard widgets
/// </summary>
public class DashboardWidgetService : IDashboardWidgetService
{
    private readonly ApplicationDbContext _context;

    public DashboardWidgetService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DashboardWidget>> GetUserWidgetsAsync(string userId)
    {
        return await _context.DashboardWidgets
            .Where(w => w.UserId == userId && w.IsVisible)
            .OrderBy(w => w.Position)
            .ToListAsync();
    }

    public async Task<DashboardWidget?> GetWidgetAsync(int widgetId, string userId)
    {
        return await _context.DashboardWidgets
            .FirstOrDefaultAsync(w => w.Id == widgetId && w.UserId == userId);
    }

    public async Task<DashboardWidget> CreateWidgetAsync(DashboardWidget widget)
    {
        widget.CreatedAt = DateTime.UtcNow;
        _context.DashboardWidgets.Add(widget);
        await _context.SaveChangesAsync();
        return widget;
    }

    public async Task<DashboardWidget> UpdateWidgetAsync(DashboardWidget widget)
    {
        widget.UpdatedAt = DateTime.UtcNow;
        _context.DashboardWidgets.Update(widget);
        await _context.SaveChangesAsync();
        return widget;
    }

    public async Task DeleteWidgetAsync(int widgetId, string userId)
    {
        var widget = await GetWidgetAsync(widgetId, userId);
        if (widget != null)
        {
            _context.DashboardWidgets.Remove(widget);
            await _context.SaveChangesAsync();
        }
    }

    public async Task ReorderWidgetsAsync(string userId, List<int> widgetIds)
    {
        var widgets = await _context.DashboardWidgets
            .Where(w => w.UserId == userId && widgetIds.Contains(w.Id))
            .ToListAsync();

        for (int i = 0; i < widgetIds.Count; i++)
        {
            var widget = widgets.FirstOrDefault(w => w.Id == widgetIds[i]);
            if (widget != null)
            {
                widget.Position = i;
                widget.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<DashboardWidget>> GetDefaultWidgetsForRole(string role)
    {
        // Define default widgets based on role
        var defaultWidgets = new List<DashboardWidget>();

        // All roles get statistics widget
        defaultWidgets.Add(new DashboardWidget
        {
            WidgetType = "Statistics",
            Title = "Document Statistics",
            Position = 0,
            IsVisible = true
        });

        // All roles get recent documents
        defaultWidgets.Add(new DashboardWidget
        {
            WidgetType = "RecentDocuments",
            Title = "Recent Documents",
            Position = 1,
            IsVisible = true
        });

        if (role == Roles.SuperAdmin || role == Roles.TenantAdmin || role == Roles.PowerUser)
        {
            // Add activity feed for admins and power users
            defaultWidgets.Add(new DashboardWidget
            {
                WidgetType = "ActivityFeed",
                Title = "Recent Activity",
                Position = 2,
                IsVisible = true
            });

            // Add saved searches widget
            defaultWidgets.Add(new DashboardWidget
            {
                WidgetType = "SavedSearches",
                Title = "Saved Searches",
                Position = 3,
                IsVisible = true
            });
        }

        if (role == Roles.SuperAdmin || role == Roles.TenantAdmin)
        {
            // Add system health widget for admins
            defaultWidgets.Add(new DashboardWidget
            {
                WidgetType = "SystemHealth",
                Title = "System Health",
                Position = 4,
                IsVisible = true
            });
        }

        return await Task.FromResult(defaultWidgets);
    }
}
