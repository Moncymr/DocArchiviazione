using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DocN.Data.Models;
using DocN.Data.Constants;

namespace DocN.Data.Services;

public class UserManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<UserManagementService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of users with their roles
    /// </summary>
    public async Task<(List<UserWithRole> Users, int TotalCount)> GetUsersAsync(
        int page = 1, 
        int pageSize = 30, 
        string? searchTerm = null, 
        string? roleFilter = null)
    {
        var query = _context.Users.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u => 
                (u.FirstName != null && u.FirstName.Contains(searchTerm)) ||
                (u.LastName != null && u.LastName.Contains(searchTerm)) ||
                (u.Email != null && u.Email.Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync();
        
        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserWithRole
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                LastLoginAt = u.LastLoginAt,
                IsActive = u.IsActive,
                TenantId = u.TenantId
            })
            .ToListAsync();

        // Get roles for each user
        foreach (var user in users)
        {
            var appUser = await _userManager.FindByIdAsync(user.Id);
            if (appUser != null)
            {
                var roles = await _userManager.GetRolesAsync(appUser);
                user.Role = roles.FirstOrDefault() ?? Roles.User;
            }
        }

        // Apply role filter if specified
        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            users = users.Where(u => u.Role == roleFilter).ToList();
        }

        return (users, totalCount);
    }

    /// <summary>
    /// Change user role
    /// </summary>
    public async Task<(bool Success, string Message)> ChangeUserRoleAsync(
        string userId, 
        string newRole, 
        string adminUserId)
    {
        try
        {
            // Validate role
            if (!Roles.All.Contains(newRole))
            {
                return (false, "Invalid role");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return (false, "User not found");
            }

            var adminUser = await _userManager.FindByIdAsync(adminUserId);
            if (adminUser == null)
            {
                return (false, "Admin user not found");
            }

            var adminRoles = await _userManager.GetRolesAsync(adminUser);
            var adminRole = adminRoles.FirstOrDefault();

            // Validation: Only SuperAdmin can promote to SuperAdmin
            if (newRole == Roles.SuperAdmin && adminRole != Roles.SuperAdmin)
            {
                return (false, "Only SuperAdmin can promote users to SuperAdmin");
            }

            // Validation: TenantAdmin cannot modify SuperAdmin
            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentRole = currentRoles.FirstOrDefault();
            
            if (currentRole == Roles.SuperAdmin && adminRole != Roles.SuperAdmin)
            {
                return (false, "TenantAdmin cannot modify SuperAdmin users");
            }

            // Validation: Cannot remove last SuperAdmin
            if (currentRole == Roles.SuperAdmin && newRole != Roles.SuperAdmin)
            {
                var superAdminCount = 0;
                var allUsers = await _userManager.Users.ToListAsync();
                foreach (var u in allUsers)
                {
                    var roles = await _userManager.GetRolesAsync(u);
                    if (roles.Contains(Roles.SuperAdmin))
                    {
                        superAdminCount++;
                    }
                }

                if (superAdminCount <= 1)
                {
                    return (false, "Cannot remove the last SuperAdmin");
                }
            }

            // Remove current roles
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            // Add new role
            await _userManager.AddToRoleAsync(user, newRole);

            // Log the action
            await LogAuditAsync(adminUserId, "RoleChanged", "User", userId, 
                $"Changed role from {currentRole} to {newRole}");

            _logger.LogInformation(
                "User {UserId} role changed from {OldRole} to {NewRole} by {AdminUserId}",
                userId, currentRole, newRole, adminUserId);

            return (true, "Role changed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing user role");
            return (false, "An error occurred while changing the role");
        }
    }

    /// <summary>
    /// Bulk change roles for multiple users
    /// </summary>
    public async Task<(int SuccessCount, int FailCount, List<string> Errors)> BulkChangeRolesAsync(
        List<string> userIds, 
        string newRole, 
        string adminUserId)
    {
        int successCount = 0;
        int failCount = 0;
        var errors = new List<string>();

        foreach (var userId in userIds)
        {
            var result = await ChangeUserRoleAsync(userId, newRole, adminUserId);
            if (result.Success)
            {
                successCount++;
            }
            else
            {
                failCount++;
                errors.Add($"User {userId}: {result.Message}");
            }
        }

        return (successCount, failCount, errors);
    }

    /// <summary>
    /// Get role statistics
    /// </summary>
    public async Task<Dictionary<string, int>> GetRoleStatisticsAsync()
    {
        var stats = new Dictionary<string, int>();
        var allUsers = await _userManager.Users.ToListAsync();

        foreach (var role in Roles.All)
        {
            stats[role] = 0;
        }

        foreach (var user in allUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var userRole = roles.FirstOrDefault() ?? Roles.User;
            if (stats.ContainsKey(userRole))
            {
                stats[userRole]++;
            }
        }

        return stats;
    }

    /// <summary>
    /// Get active users in last N days
    /// </summary>
    public async Task<(int ActiveUsers, int InactiveUsers)> GetActiveUsersStatsAsync(int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        
        var activeCount = await _context.Users
            .CountAsync(u => u.LastLoginAt != null && u.LastLoginAt >= cutoffDate);
        
        var totalCount = await _context.Users.CountAsync();
        
        return (activeCount, totalCount - activeCount);
    }

    private async Task LogAuditAsync(string userId, string action, string resourceType, 
        string resourceId, string details)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    public class UserWithRole
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Role { get; set; } = Roles.User;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }
        public int? TenantId { get; set; }
    }
}
