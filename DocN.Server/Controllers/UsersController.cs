using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DocN.Data;
using DocN.Data.Models;
using DocN.Data.Constants;

namespace DocN.Server.Controllers;

/// <summary>
/// Controller for user management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,TenantAdmin")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        try
        {
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var userDtos = new List<UserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new UserDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = roles.FirstOrDefault(),
                    IsActive = user.IsActive,
                    LastLoginAt = user.LastLoginAt
                });
            }

            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { error = "Error retrieving users" });
        }
    }

    /// <summary>
    /// Update user role
    /// </summary>
    [HttpPut("{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(
        string userId, 
        [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            // Validate role
            if (!Roles.All.Contains(request.Role))
            {
                return BadRequest(new { error = "Invalid role" });
            }

            // Remove current roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            // Add new role
            var result = await _userManager.AddToRoleAsync(user, request.Role);
            if (!result.Succeeded)
            {
                return BadRequest(new { error = "Failed to update role", details = result.Errors });
            }

            _logger.LogInformation(
                "User {UserId} role updated to {Role} by {AdminId}",
                userId, request.Role, User.Identity?.Name);

            return Ok(new { message = "Role updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role");
            return StatusCode(500, new { error = "Error updating user role" });
        }
    }

    /// <summary>
    /// Update user active status
    /// </summary>
    [HttpPut("{userId}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        string userId,
        [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            user.IsActive = request.IsActive;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new { error = "Failed to update user status", details = result.Errors });
            }

            _logger.LogInformation(
                "User {UserId} status updated to {IsActive} by {AdminId}",
                userId, request.IsActive, User.Identity?.Name);

            return Ok(new { message = "User status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user status");
            return StatusCode(500, new { error = "Error updating user status" });
        }
    }

    // DTOs
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class UpdateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateStatusRequest
    {
        public bool IsActive { get; set; }
    }
}
