using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DocN.Data.Services;
using DocN.Data.Constants;
using System.Security.Claims;

namespace DocN.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserManagementController : ControllerBase
{
    private readonly UserManagementService _userManagementService;
    private readonly ILogger<UserManagementController> _logger;

    public UserManagementController(
        UserManagementService userManagementService,
        ILogger<UserManagementController> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of users
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "AdminUsers")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? roleFilter = null)
    {
        try
        {
            var (users, totalCount) = await _userManagementService.GetUsersAsync(
                page, pageSize, searchTerm, roleFilter);

            return Ok(new
            {
                users,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, "An error occurred while getting users");
        }
    }

    /// <summary>
    /// Change user role
    /// </summary>
    [HttpPost("{userId}/change-role")]
    [Authorize(Policy = "AdminRoles")]
    public async Task<IActionResult> ChangeRole(
        string userId,
        [FromBody] ChangeRoleRequest request)
    {
        try
        {
            var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(adminUserId))
            {
                return Unauthorized();
            }

            var result = await _userManagementService.ChangeUserRoleAsync(
                userId, request.NewRole, adminUserId);

            if (result.Success)
            {
                return Ok(new { message = result.Message });
            }
            else
            {
                return BadRequest(new { message = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing user role");
            return StatusCode(500, "An error occurred while changing the role");
        }
    }

    /// <summary>
    /// Bulk change roles
    /// </summary>
    [HttpPost("bulk-change-roles")]
    [Authorize(Policy = "AdminRoles")]
    public async Task<IActionResult> BulkChangeRoles([FromBody] BulkChangeRolesRequest request)
    {
        try
        {
            var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(adminUserId))
            {
                return Unauthorized();
            }

            var (successCount, failCount, errors) = await _userManagementService
                .BulkChangeRolesAsync(request.UserIds, request.NewRole, adminUserId);

            return Ok(new
            {
                successCount,
                failCount,
                errors,
                message = $"Changed {successCount} users successfully, {failCount} failed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk role change");
            return StatusCode(500, "An error occurred during bulk role change");
        }
    }

    /// <summary>
    /// Get role statistics
    /// </summary>
    [HttpGet("role-stats")]
    [Authorize(Policy = "AdminUsers")]
    public async Task<IActionResult> GetRoleStats()
    {
        try
        {
            var stats = await _userManagementService.GetRoleStatisticsAsync();
            var (activeUsers, inactiveUsers) = await _userManagementService
                .GetActiveUsersStatsAsync(30);

            return Ok(new
            {
                roleDistribution = stats,
                activeUsers,
                inactiveUsers,
                totalUsers = activeUsers + inactiveUsers
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role statistics");
            return StatusCode(500, "An error occurred while getting statistics");
        }
    }

    /// <summary>
    /// Get available roles with permissions
    /// </summary>
    [HttpGet("available-roles")]
    [Authorize(Policy = "AdminUsers")]
    public IActionResult GetAvailableRoles()
    {
        var roles = Roles.All.Select(role => new
        {
            name = role,
            permissions = Permissions.GetPermissionsForRole(role)
        });

        return Ok(roles);
    }

    public class ChangeRoleRequest
    {
        public string NewRole { get; set; } = string.Empty;
    }

    public class BulkChangeRolesRequest
    {
        public List<string> UserIds { get; set; } = new();
        public string NewRole { get; set; } = string.Empty;
    }
}
