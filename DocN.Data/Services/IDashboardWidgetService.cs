using DocN.Data.Models;

namespace DocN.Data.Services;

/// <summary>
/// Service interface for managing dashboard widgets
/// </summary>
public interface IDashboardWidgetService
{
    Task<List<DashboardWidget>> GetUserWidgetsAsync(string userId);
    Task<DashboardWidget?> GetWidgetAsync(int widgetId, string userId);
    Task<DashboardWidget> CreateWidgetAsync(DashboardWidget widget);
    Task<DashboardWidget> UpdateWidgetAsync(DashboardWidget widget);
    Task DeleteWidgetAsync(int widgetId, string userId);
    Task ReorderWidgetsAsync(string userId, List<int> widgetIds);
    Task<List<DashboardWidget>> GetDefaultWidgetsForRole(string role);
}
