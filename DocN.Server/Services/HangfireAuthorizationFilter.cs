using Hangfire.Dashboard;

namespace DocN.Server.Services;

/// <summary>
/// Authorization filter for Hangfire dashboard
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Authorize access to Hangfire dashboard
    /// In production, implement proper authentication/authorization
    /// </summary>
    public bool Authorize(DashboardContext context)
    {
        // In development, allow all access
        // In production, check user roles/claims
        var httpContext = context.GetHttpContext();
        
        // TODO: Add proper authorization in production
        // Example: return httpContext.User.IsInRole("Admin");
        
        // For now, allow access in development mode only
        return httpContext.Request.Host.Host == "localhost" 
            || httpContext.Request.Host.Host == "127.0.0.1";
    }
}
