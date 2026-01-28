using System.Net.Http.Json;

namespace DocN.Client.Services;

/// <summary>
/// Authentication service that communicates with Server API
/// Handles login, registration, and logout operations via HTTP
/// </summary>
public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(string email, string password, bool rememberMe);
    Task<RegisterResult> RegisterAsync(string firstName, string lastName, string email, string password);
    Task<bool> LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
}

public class AuthenticationService : IAuthenticationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(IHttpClientFactory httpClientFactory, ILogger<AuthenticationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate user via Server API
    /// </summary>
    public async Task<LoginResult> LoginAsync(string email, string password, bool rememberMe)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Email = email,
                Password = password,
                RememberMe = rememberMe
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                _logger.LogInformation("Login successful for {Email}", email);
                
                return new LoginResult 
                { 
                    Success = true, 
                    UserId = result?.UserId,
                    Email = result?.Email,
                    FirstName = result?.FirstName,
                    LastName = result?.LastName
                };
            }
            else
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                var errorMessage = error?.Error ?? "Login failed";
                
                _logger.LogWarning("Login failed for {Email}: {Error}", email, errorMessage);
                
                return new LoginResult 
                { 
                    Success = false, 
                    ErrorMessage = errorMessage,
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during login for {Email}", email);
            return new LoginResult 
            { 
                Success = false, 
                ErrorMessage = "Unable to connect to server. Please check your connection." 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {Email}", email);
            return new LoginResult 
            { 
                Success = false, 
                ErrorMessage = "An unexpected error occurred. Please try again." 
            };
        }
    }

    /// <summary>
    /// Register new user via Server API
    /// </summary>
    public async Task<RegisterResult> RegisterAsync(string firstName, string lastName, string email, string password)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.PostAsJsonAsync("/api/auth/register", new
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Password = password
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
                _logger.LogInformation("Registration successful for {Email}", email);
                
                return new RegisterResult 
                { 
                    Success = true, 
                    UserId = result?.UserId,
                    Email = result?.Email
                };
            }
            else
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                var errorMessage = error?.Error ?? "Registration failed";
                
                _logger.LogWarning("Registration failed for {Email}: {Error}", email, errorMessage);
                
                return new RegisterResult 
                { 
                    Success = false, 
                    ErrorMessage = errorMessage,
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during registration for {Email}", email);
            return new RegisterResult 
            { 
                Success = false, 
                ErrorMessage = "Unable to connect to server. Please check your connection." 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration for {Email}", email);
            return new RegisterResult 
            { 
                Success = false, 
                ErrorMessage = "An unexpected error occurred. Please try again." 
            };
        }
    }

    /// <summary>
    /// Logout current user via Server API
    /// </summary>
    public async Task<bool> LogoutAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.PostAsync("/api/auth/logout", null);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Logout successful");
                return true;
            }
            else
            {
                _logger.LogWarning("Logout request returned status code: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return false;
        }
    }

    /// <summary>
    /// Check if user is currently authenticated
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendAPI");
            var response = await client.GetAsync("/api/auth/status");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthStatusResponse>();
                return result?.IsAuthenticated ?? false;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication status");
            return false;
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// Response Models
// ════════════════════════════════════════════════════════════════════════════════

public class LoginResult
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
}

public class RegisterResult
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
}

// Internal response models for deserialization
internal record LoginResponse(bool Success, string? UserId, string? Email, string? FirstName, string? LastName);
internal record RegisterResponse(bool Success, string? UserId, string? Email);
internal record ErrorResponse(string? Error);
internal record AuthStatusResponse(bool IsAuthenticated, string? UserName);
