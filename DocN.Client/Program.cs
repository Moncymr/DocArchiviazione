using DocN.Client.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using DocN.Data.Models;
using DocN.Client.Services;

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
    client.BaseAddress = new Uri(builder.Configuration["BackendApiUrl"] ?? "https://localhost:5211/");
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Register Authentication Service to call Server API for login/register/logout
builder.Services.AddScoped<DocN.Client.Services.IAuthenticationService, DocN.Client.Services.AuthenticationService>();

var app = builder.Build();

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

app.Run();
