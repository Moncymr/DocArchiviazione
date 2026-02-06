using DocN.Client.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using DocN.Data.Models;
using DocN.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;

// Helper method to ensure configuration files exist
static void EnsureConfigurationFiles()
{
    var baseDirectory = AppContext.BaseDirectory;
    var appsettingsPath = Path.Combine(baseDirectory, "appsettings.json");
    var appsettingsDevPath = Path.Combine(baseDirectory, "appsettings.Development.json");
    var appsettingsExamplePath = Path.Combine(baseDirectory, "appsettings.example.json");
    var appsettingsDevExamplePath = Path.Combine(baseDirectory, "appsettings.Development.example.json");

    // Create appsettings.json if it doesn't exist
    if (!File.Exists(appsettingsPath))
    {
        try
        {
            if (File.Exists(appsettingsExamplePath))
            {
                File.Copy(appsettingsExamplePath, appsettingsPath);
                Console.WriteLine($"Created {appsettingsPath} from example file. Please update the configuration with your settings.");
            }
            else
            {
                // Create a minimal configuration file
                var minimalConfig = @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }
  },
  ""AllowedHosts"": ""*"",
  ""ConnectionStrings"": {
    ""DefaultConnection"": ""Server=localhost;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True""
  },
  ""BackendApiUrl"": ""https://localhost:5211/"",
  ""FileStorage"": {
    ""UploadPath"": ""Uploads"",
    ""MaxFileSizeInMB"": 100,
    ""AllowedExtensions"": [ "".pdf"", "".doc"", "".docx"", "".txt"", "".jpg"", "".jpeg"", "".png"" ]
  }
}";
                File.WriteAllText(appsettingsPath, minimalConfig);
                Console.WriteLine($"Created minimal {appsettingsPath}. Please update with your database connection string.");
            }
        }
        catch (IOException ex)
        {
            // File might be being created by another process (e.g., Server starting at same time)
            // Wait a moment and check if it exists now
            Thread.Sleep(100);
            if (!File.Exists(appsettingsPath))
            {
                Console.WriteLine($"Warning: Could not create {appsettingsPath}: {ex.Message}");
            }
        }
    }

    // Create appsettings.Development.json if it doesn't exist
    if (!File.Exists(appsettingsDevPath))
    {
        try
        {
            if (File.Exists(appsettingsDevExamplePath))
            {
                File.Copy(appsettingsDevExamplePath, appsettingsDevPath);
                Console.WriteLine($"Created {appsettingsDevPath} from example file.");
            }
            else
            {
                // Create a minimal development configuration
                var minimalDevConfig = @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }
  },
  ""ConnectionStrings"": {
    ""DefaultConnection"": ""Server=localhost;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True""
  }
}";
                File.WriteAllText(appsettingsDevPath, minimalDevConfig);
                Console.WriteLine($"Created minimal {appsettingsDevPath}.");
            }
        }
        catch (IOException ex)
        {
            // File might be being created by another process
            Thread.Sleep(100);
            if (!File.Exists(appsettingsDevPath))
            {
                Console.WriteLine($"Warning: Could not create {appsettingsDevPath}: {ex.Message}");
            }
        }
    }
}

// Ensure configuration files exist before starting
EnsureConfigurationFiles();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add FluentUI Blazor components
builder.Services.AddFluentUIComponents();

// Add HttpClient for Blazor components
builder.Services.AddHttpClient();

// Add memory cache for client-side caching (optional)
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024 * 1024 * 50; // 50MB cache limit for client-side data
});

// ═══════════════════════════════════════════════════════════════════════════════
// CLIENT AUTHENTICATION (Cookie-Based, No Direct Database Access)
// ═══════════════════════════════════════════════════════════════════════════════
// The Client is a Blazor Server UI that should NOT access the database directly.
// Authentication is handled via cookies from the Server's login endpoints.
// 
// IMPORTANT: Do NOT register ApplicationDbContext or AddEntityFrameworkStores here!
// That causes EF Core model validation during builder.Build() which crashes the app
// when database schema doesn't match (e.g., missing Notifications tables).
// 
// The Server handles all database operations. The Client just authenticates users
// via HTTP requests and uses cookies for subsequent requests.
// ═══════════════════════════════════════════════════════════════════════════════

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });

builder.Services.AddCascadingAuthenticationState();

// ═══════════════════════════════════════════════════════════════════════════════
// CLIENT-SIDE SERVICES (NO DATABASE ACCESS)
// ═══════════════════════════════════════════════════════════════════════════════
// The Client should ONLY have UI services and HTTP clients.
// All data services are in the Server and accessed via HTTP APIs.
// 
// REMOVED: All database-dependent services that were causing DI errors:
// - DocumentService, EmbeddingService, CategoryService
// - DocumentStatisticsService, MultiProviderAIService
// - DashboardWidgetService, SavedSearchService, SearchSuggestionService
// - UserActivityService, LogService, SemanticRAGService
// 
// These services require ApplicationDbContext which is not available in Client.
// Client components should call Server HTTP APIs instead of using services directly.
// ═══════════════════════════════════════════════════════════════════════════════

// Application Settings (for configuration only, not for data services)
builder.Services.Configure<FileStorageSettings>(builder.Configuration.GetSection("FileStorage"));

// Notification Service for real-time updates (SignalR client-side only)
builder.Services.AddScoped<DocN.Client.Services.NotificationClientService>();

// Authentication State Provider - Manages user authentication state in Client
// This provider stores user information in browser session storage and provides
// authentication state to all components via AuthorizeView and AuthenticationStateProvider
builder.Services.AddScoped<DocN.Client.Services.CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => 
    provider.GetRequiredService<DocN.Client.Services.CustomAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

// NOTE: All data operations should be performed via HttpClient calls to Server APIs
// Example: Instead of injecting IDocumentService, use HttpClient to call /api/documents

// NOTE: Database seeding is handled by the Server, not the Client
// The Client is a Blazor WebAssembly application and should only communicate with the Server via HTTP APIs
// Direct database access from the Client can cause race conditions and conflicts when both start simultaneously

// Configure HttpClient to call the backend API with extended timeout for AI operations
// Increased timeout to 300 seconds (5 minutes) for AI/RAG operations which can take longer
// This matches the server-side timeout configuration for AI providers
builder.Services.AddHttpClient("BackendAPI", client =>
{
    var backendUrl = builder.Configuration["BackendApiUrl"] ?? "https://localhost:5211/";
    client.BaseAddress = new Uri(backendUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
    Console.WriteLine($"BackendAPI HttpClient configured with BaseAddress: {backendUrl}");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    // In development, bypass SSL certificate validation to avoid issues with self-signed certificates
    // This is ONLY for development - production should use valid certificates
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        Console.WriteLine("SSL certificate validation bypassed for development environment");
    }
    return handler;
});

// Register Authentication Service to call Server API for login/register/logout
builder.Services.AddScoped<DocN.Client.Services.IAuthenticationService, DocN.Client.Services.AuthenticationService>();

// Register Server Health Check Service for startup validation
builder.Services.AddSingleton<DocN.Client.Services.IServerHealthCheckService, DocN.Client.Services.ServerHealthCheckService>();

WebApplication app;
try
{
    app = builder.Build();
}
catch (Exception ex)
{
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    Console.WriteLine("FATAL ERROR: Failed to build the application");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    Console.WriteLine($"Exception Type: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
    
    if (ex.InnerException != null)
    {
        Console.WriteLine("\nInner Exception:");
        Console.WriteLine($"Type: {ex.InnerException.GetType().Name}");
        Console.WriteLine($"Message: {ex.InnerException.Message}");
        Console.WriteLine($"Stack Trace:\n{ex.InnerException.StackTrace}");
    }
    
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

// ═══════════════════════════════════════════════════════════════════════════════
// SERVER AVAILABILITY CHECK
// ═══════════════════════════════════════════════════════════════════════════════
// When starting from Visual Studio with multiple startup projects (F5), both Client 
// and Server start simultaneously. The Client needs to wait for the Server to be ready.
// 
// This check ensures the Server is available before the Client starts accepting requests.
// Without this, users may see "Unable to connect to server" errors during startup.
// ═══════════════════════════════════════════════════════════════════════════════

try
{
    var healthCheckService = app.Services.GetService<DocN.Client.Services.IServerHealthCheckService>();
    
    if (healthCheckService == null)
    {
        app.Logger.LogWarning("Server health check service not available. Skipping health check.");
        Console.WriteLine("⚠️  Warning: Server health check service not configured. Continuing startup...");
    }
    else
    {
        Console.WriteLine("════════════════════════════════════════════════════════════════════");
        Console.WriteLine("Checking Server availability...");
        Console.WriteLine("════════════════════════════════════════════════════════════════════");
        
        // Wait for Server to be available (30 retries * ~1-5 seconds = max 2.5 minutes)
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var serverAvailable = await healthCheckService.WaitForServerAsync(
            maxRetries: 30, 
            delayMs: 1000, 
            cancellationToken: cts.Token
        );
        
        if (!serverAvailable)
        {
            Console.WriteLine("════════════════════════════════════════════════════════════════════");
            Console.WriteLine("⚠️  WARNING: Server is not available");
            Console.WriteLine("════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("The Server API is not responding. The Client will start anyway,");
            Console.WriteLine("but features that require the Server will not work.");
            Console.WriteLine();
            Console.WriteLine("Please ensure the Server is running:");
            Console.WriteLine($"  - Server should be at: {builder.Configuration["BackendApiUrl"] ?? "https://localhost:5211/"}");
            Console.WriteLine("  - Check Server console for errors");
            Console.WriteLine("  - Verify database connection is configured");
            Console.WriteLine();
            Console.WriteLine("════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("════════════════════════════════════════════════════════════════════");
            Console.WriteLine("✅ Server is available and ready");
            Console.WriteLine("════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
        }
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not check Server availability. Client will start anyway.");
    Console.WriteLine($"⚠️  Warning: Server health check failed: {ex.Message}");
    Console.WriteLine($"   Exception Type: {ex.GetType().Name}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner Exception: {ex.InnerException.Message}");
    }
    Console.WriteLine("Client will start anyway, but Server connectivity may be limited.");
}

// NOTE: Database seeding removed from Client
// ═══════════════════════════════════════════════════════════════════════════════
// The Server is responsible for all database operations including seeding.
// The Client should ONLY communicate with the Server via HTTP APIs.
// 
// Previous implementation had both Client and Server seeding the database,
// which caused race conditions, conflicts, and crashes when starting simultaneously.
// 
// If you see database-related errors in the Client:
// 1. Ensure the Server is running and has completed seeding
// 2. Check the Server logs for seeding success/failure
// 3. The Client will automatically work once the Server has seeded the database
// ═══════════════════════════════════════════════════════════════════════════════

// Ensure upload directory exists
var fileStorageSettings = builder.Configuration.GetSection("FileStorage").Get<FileStorageSettings>();
if (fileStorageSettings != null && !string.IsNullOrEmpty(fileStorageSettings.UploadPath))
{
    try
    {
        // Ensure the path is safe and create directory
        var uploadPath = Path.GetFullPath(fileStorageSettings.UploadPath);
        Directory.CreateDirectory(uploadPath);
        app.Logger.LogInformation("Upload directory created/verified: {UploadPath}", uploadPath);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to create upload directory. File uploads may not work correctly.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Authentication endpoints
// NOTE: Authentication endpoints (login, logout, register) are handled by the Server.
// The Client should submit forms to the Server's authentication endpoints, not handle them locally.
// The Server will manage Identity services (UserManager, SignInManager) and return authentication cookies.

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

try
{
    Console.WriteLine("Starting the application...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    Console.WriteLine("FATAL ERROR: Application crashed during execution");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    Console.WriteLine($"Exception Type: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
    
    if (ex.InnerException != null)
    {
        Console.WriteLine("\nInner Exception:");
        Console.WriteLine($"Type: {ex.InnerException.GetType().Name}");
        Console.WriteLine($"Message: {ex.InnerException.Message}");
        Console.WriteLine($"Stack Trace:\n{ex.InnerException.StackTrace}");
    }
    
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    
    // Log to a file as well for easier debugging
    var errorLogPath = Path.Combine(AppContext.BaseDirectory, "client-crash.log");
    try
    {
        var errorLog = $@"
Client Crash Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}
═══════════════════════════════════════════════════════════════════
Exception Type: {ex.GetType().Name}
Message: {ex.Message}
Stack Trace:
{ex.StackTrace}

{(ex.InnerException != null ? $@"Inner Exception:
Type: {ex.InnerException.GetType().Name}
Message: {ex.InnerException.Message}
Stack Trace:
{ex.InnerException.StackTrace}
" : "")}
═══════════════════════════════════════════════════════════════════
";
        File.WriteAllText(errorLogPath, errorLog);
        Console.WriteLine($"\nError details have been written to: {errorLogPath}");
    }
    catch
    {
        // Ignore file writing errors
    }
    
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
    throw; // Re-throw to ensure proper exit code
}
