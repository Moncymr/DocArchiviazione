using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DocN.Data.Models;

namespace DocN.Server.Controllers;

/// <summary>
/// Authentication API Controller
/// Handles user login, registration, and logout operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate user with email and password
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Login result with user information</returns>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Login attempt with empty credentials");
            return BadRequest(new { error = "Email and password are required" });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true
        );

        if (result.Succeeded)
        {
            // Update last login time
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            
            _logger.LogInformation("User {Email} logged in successfully", request.Email);
            return Ok(new 
            { 
                success = true, 
                userId = user.Id, 
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName
            });
        }
        else if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked: {Email}", request.Email);
            return StatusCode(StatusCodes.Status423Locked, new { error = "Account is locked due to multiple failed login attempts" });
        }
        else if (result.IsNotAllowed)
        {
            _logger.LogWarning("User login not allowed: {Email}", request.Email);
            return Unauthorized(new { error = "Login not allowed. Please confirm your email." });
        }
        else
        {
            _logger.LogWarning("Failed login attempt for: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }
    }

    /// <summary>
    /// Register new user account
    /// </summary>
    /// <param name="request">Registration information</param>
    /// <returns>Registration result with user ID</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FirstName) || 
            string.IsNullOrWhiteSpace(request.LastName))
        {
            _logger.LogWarning("Registration attempt with incomplete data");
            return BadRequest(new { error = "All fields are required" });
        }

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", request.Email);
            return Conflict(new { error = "A user with this email already exists" });
        }

        // Create new user
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            _logger.LogInformation("New user registered: {Email}", request.Email);
            
            // Automatically sign in the user after registration
            await _signInManager.SignInAsync(user, isPersistent: false);
            
            return Ok(new 
            { 
                success = true, 
                userId = user.Id,
                email = user.Email,
                message = "Registration successful" 
            });
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, errors);
            return BadRequest(new { error = $"Registration failed: {errors}" });
        }
    }

    /// <summary>
    /// Log out current user
    /// </summary>
    /// <returns>Logout confirmation</returns>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out");
        return Ok(new { success = true, message = "Logged out successfully" });
    }

    /// <summary>
    /// Check if user is authenticated
    /// </summary>
    /// <returns>Authentication status</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetAuthStatus()
    {
        var isAuthenticated = User?.Identity?.IsAuthenticated ?? false;
        return Ok(new 
        { 
            isAuthenticated,
            userName = User?.Identity?.Name
        });
    }
}

/// <summary>
/// Login request model
/// </summary>
/// <param name="Email">User email address</param>
/// <param name="Password">User password</param>
/// <param name="RememberMe">Keep user logged in</param>
public record LoginRequest(string Email, string Password, bool RememberMe);

/// <summary>
/// Registration request model
/// </summary>
/// <param name="FirstName">User first name</param>
/// <param name="LastName">User last name</param>
/// <param name="Email">User email address</param>
/// <param name="Password">User password</param>
public record RegisterRequest(string FirstName, string LastName, string Email, string Password);
