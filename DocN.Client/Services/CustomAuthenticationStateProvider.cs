using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace DocN.Client.Services;

/// <summary>
/// Custom authentication state provider that stores user information in server-side session
/// Compatible with static server-side rendering (no Interactive Server required)
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;
    private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());
    private const string UserSessionKey = "userSession";

    public CustomAuthenticationStateProvider(
        IHttpContextAccessor httpContextAccessor,
        ILogger<CustomAuthenticationStateProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current authentication state
    /// Reads user info from server-side session and creates claims principal
    /// </summary>
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogDebug("No HttpContext available");
                return Task.FromResult(new AuthenticationState(_anonymous));
            }

            var userSessionJson = httpContext.Session.GetString(UserSessionKey);
            
            if (string.IsNullOrEmpty(userSessionJson))
            {
                _logger.LogDebug("No user session found in storage");
                return Task.FromResult(new AuthenticationState(_anonymous));
            }

            var userSession = JsonSerializer.Deserialize<UserSession>(userSessionJson);
            if (userSession == null)
            {
                _logger.LogDebug("Failed to deserialize user session");
                return Task.FromResult(new AuthenticationState(_anonymous));
            }
            
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userSession.UserId),
                new Claim(ClaimTypes.Name, $"{userSession.FirstName} {userSession.LastName}"),
                new Claim(ClaimTypes.Email, userSession.Email),
                new Claim("FirstName", userSession.FirstName),
                new Claim("LastName", userSession.LastName)
            }, "CustomAuth"));

            _logger.LogInformation("User authenticated: {Email}", userSession.Email);
            return Task.FromResult(new AuthenticationState(claimsPrincipal));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving authentication state");
            return Task.FromResult(new AuthenticationState(_anonymous));
        }
    }

    /// <summary>
    /// Sets the user as authenticated and stores information in server-side session
    /// Called after successful login or registration
    /// </summary>
    public Task SetAuthenticationStateAsync(string userId, string email, string firstName, string lastName)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogError("No HttpContext available to set authentication state");
                throw new InvalidOperationException("No HttpContext available");
            }

            var userSession = new UserSession
            {
                UserId = userId,
                Email = email,
                FirstName = firstName,
                LastName = lastName
            };

            var userSessionJson = JsonSerializer.Serialize(userSession);
            httpContext.Session.SetString(UserSessionKey, userSessionJson);

            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, $"{firstName} {lastName}"),
                new Claim(ClaimTypes.Email, email),
                new Claim("FirstName", firstName),
                new Claim("LastName", lastName)
            }, "CustomAuth"));

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
            _logger.LogInformation("Authentication state set for user: {Email}", email);
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting authentication state");
            throw;
        }
    }

    /// <summary>
    /// Clears the authentication state (logout)
    /// Removes user information from server-side session
    /// </summary>
    public Task ClearAuthenticationStateAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                httpContext.Session.Remove(UserSessionKey);
            }
            
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
            _logger.LogInformation("Authentication state cleared (user logged out)");
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing authentication state");
            throw;
        }
    }
}

/// <summary>
/// User session data stored in server-side session
/// </summary>
public class UserSession
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
