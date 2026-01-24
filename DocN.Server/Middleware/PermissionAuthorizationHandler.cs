using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using DocN.Data.Constants;

namespace DocN.Server.Middleware;

/// <summary>
/// Authorization handler for permission-based access control
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Get user's role
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(role))
        {
            return Task.CompletedTask;
        }

        // Get permissions for the role
        var userPermissions = Permissions.GetPermissionsForRole(role);

        // Check if user has the required permission
        foreach (var requiredPermission in requirement.Permissions)
        {
            // Check for exact match
            if (userPermissions.Contains(requiredPermission))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Check for wildcard permissions (e.g., admin.* covers admin.users)
            if (userPermissions.Any(p => p.EndsWith(".*") && 
                requiredPermission.StartsWith(p[..^2])))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Requirement for permission-based authorization
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string[] Permissions { get; }

    public PermissionRequirement(params string[] permissions)
    {
        Permissions = permissions;
    }
}
