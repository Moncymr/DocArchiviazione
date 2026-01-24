using Microsoft.AspNetCore.Authorization;

namespace DocN.Server.Middleware;

/// <summary>
/// Authorization attribute for granular permission-based access control
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(params string[] permissions)
    {
        Policy = string.Join(",", permissions);
    }
}
