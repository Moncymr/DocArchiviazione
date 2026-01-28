using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Claims;

namespace DocN.Client.Services;

/// <summary>
/// Custom authentication state provider that stores user information in browser session storage
/// This allows the client to maintain authentication state independently of server cookies
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;
    private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());

    public CustomAuthenticationStateProvider(
        ProtectedSessionStorage sessionStorage,
        ILogger<CustomAuthenticationStateProvider> logger)
    {
        _sessionStorage = sessionStorage;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current authentication state
    /// Reads user info from session storage and creates claims principal
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var userSessionResult = await _sessionStorage.GetAsync<UserSession>("userSession");
            
            if (!userSessionResult.Success || userSessionResult.Value == null)
            {
                _logger.LogDebug("No user session found in storage");
                return new AuthenticationState(_anonymous);
            }

            var userSession = userSessionResult.Value;
            
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userSession.UserId),
                new Claim(ClaimTypes.Name, $"{userSession.FirstName} {userSession.LastName}"),
                new Claim(ClaimTypes.Email, userSession.Email),
                new Claim("FirstName", userSession.FirstName),
                new Claim("LastName", userSession.LastName)
            }, "CustomAuth"));

            _logger.LogInformation("User authenticated: {Email}", userSession.Email);
            return new AuthenticationState(claimsPrincipal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving authentication state");
            return new AuthenticationState(_anonymous);
        }
    }

    /// <summary>
    /// Sets the user as authenticated and stores information in session storage
    /// Called after successful login or registration
    /// </summary>
    public async Task SetAuthenticationStateAsync(string userId, string email, string firstName, string lastName)
    {
        try
        {
            var userSession = new UserSession
            {
                UserId = userId,
                Email = email,
                FirstName = firstName,
                LastName = lastName
            };

            await _sessionStorage.SetAsync("userSession", userSession);

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting authentication state");
            throw;
        }
    }

    /// <summary>
    /// Clears the authentication state (logout)
    /// Removes user information from session storage
    /// </summary>
    public async Task ClearAuthenticationStateAsync()
    {
        try
        {
            await _sessionStorage.DeleteAsync("userSession");
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
            _logger.LogInformation("Authentication state cleared (user logged out)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing authentication state");
            throw;
        }
    }
}

/// <summary>
/// User session data stored in browser storage
/// </summary>
public class UserSession
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
