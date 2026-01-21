using DocN.Client.Components;
using DocN.Core.Interfaces;
using DocN.Data;
using DocN.Data.Models;
using DocN.Data.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
#pragma warning disable SKEXP0010 // Method is for evaluation purposes only
#pragma warning disable SKEXP0110 // Agents are experimental

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

// Add HttpClient for Blazor components
builder.Services.AddHttpClient();

// Add memory cache for caching service
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024 * 1024 * 100; // 100MB cache limit
});

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Use a fallback connection string only in development
if (string.IsNullOrEmpty(connectionString))
{
    if (builder.Environment.IsDevelopment())
    {
        connectionString = "Server=NTSPJ-MRM2\\SQL2025;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True";
    }
    else
    {
        throw new InvalidOperationException("Database connection string 'DefaultConnection' is not configured. Please set it in appsettings.json or environment variables.");
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptions =>
    {
        // Enable retry on transient failures
        sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });

    // Enable sensitive data logging in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Add DocArcContext for logging
builder.Services.AddDbContext<DocArcContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptions =>
    {
        sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Identity & Authentication
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy configuration
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;

    // Lockout configuration
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddCascadingAuthenticationState();

// Application Settings
builder.Services.Configure<FileStorageSettings>(builder.Configuration.GetSection("FileStorage"));
builder.Services.Configure<AISettings>(builder.Configuration.GetSection("AI"));
builder.Services.Configure<OpenAISettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("Gemini"));
builder.Services.Configure<EmbeddingsSettings>(builder.Configuration.GetSection("Embeddings"));

// Application Services
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<DocN.Data.Services.IChunkingService, DocN.Data.Services.ChunkingService>();
builder.Services.AddScoped<DocN.Data.Services.IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<DocN.Data.Services.ICategoryService, CategoryService>();
builder.Services.AddScoped<IDocumentStatisticsService, DocumentStatisticsService>();
builder.Services.AddScoped<IMultiProviderAIService, MultiProviderAIService>();
builder.Services.AddScoped<IOCRService, TesseractOCRService>();
builder.Services.AddScoped<IFileProcessingService, FileProcessingService>();
builder.Services.AddScoped<ILogService, LogService>();

// Configure Semantic Kernel for RAG Service (only if AI services are configured)
var azureOpenAIEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var azureOpenAIKey = builder.Configuration["AzureOpenAI:ApiKey"];
var azureOpenAIChatDeployment = builder.Configuration["AzureOpenAI:ChatDeployment"] ?? "gpt-4";
var azureOpenAIEmbeddingDeployment = builder.Configuration["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-ada-002";
var openAIKey = builder.Configuration["OpenAI:ApiKey"];

var hasAIServiceConfigured = (!string.IsNullOrEmpty(azureOpenAIEndpoint) && !string.IsNullOrEmpty(azureOpenAIKey)) ||
                             !string.IsNullOrEmpty(openAIKey);

if (hasAIServiceConfigured)
{
    var kernelBuilder = Kernel.CreateBuilder();

    if (!string.IsNullOrEmpty(azureOpenAIEndpoint) && !string.IsNullOrEmpty(azureOpenAIKey))
    {
        // Add Azure OpenAI Chat Completion
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: azureOpenAIChatDeployment,
            endpoint: azureOpenAIEndpoint,
            apiKey: azureOpenAIKey);

        // Add Azure OpenAI Text Embedding
        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
            deploymentName: azureOpenAIEmbeddingDeployment,
            endpoint: azureOpenAIEndpoint,
            apiKey: azureOpenAIKey);
    }
    else if (!string.IsNullOrEmpty(openAIKey))
    {
        // Fallback to OpenAI if Azure is not configured
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: "gpt-4",
            apiKey: openAIKey);

        kernelBuilder.AddOpenAITextEmbeddingGeneration(
            modelId: "text-embedding-ada-002",
            apiKey: openAIKey);
    }

    var kernel = kernelBuilder.Build();
    builder.Services.AddSingleton(kernel);

    // Register additional services for RAG
    builder.Services.AddScoped<DocN.Data.Services.IChunkingService, ChunkingService>();
    builder.Services.AddScoped<ICacheService, CacheService>();

    // Register Semantic RAG Service only when AI services are configured
    builder.Services.AddScoped<ISemanticRAGService, SemanticRAGService>();
}
else
{
    // Register a no-op implementation when no AI service is configured
    builder.Services.AddScoped<DocN.Data.Services.IChunkingService, ChunkingService>();
    builder.Services.AddScoped<ICacheService, CacheService>();

    // Register a no-op service that returns empty results when AI is not configured
    builder.Services.AddScoped<ISemanticRAGService, NoOpSemanticRAGService>();
}

// Register ApplicationSeeder
builder.Services.AddScoped<DocN.Data.Services.ApplicationSeeder>();

// Configure HttpClient to call the backend API with extended timeout for AI operations
// Increased timeout to 300 seconds (5 minutes) for AI/RAG operations which can take longer
// This matches the server-side timeout configuration for AI providers
builder.Services.AddHttpClient("BackendAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BackendApiUrl"] ?? "https://localhost:5211/");
    client.Timeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

// Seed the database with default tenant and user
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DocN.Data.Services.ApplicationSeeder>();
        await seeder.SeedAsync();
        logger.LogInformation("Database seeding completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database. The application will continue but may not function correctly without initial data.\n" +
            "Please verify:\n" +
            "1. Database connection string is correct and database server is accessible\n" +
            "2. Database has been created using the SQL scripts in Database/ folder\n" +
            "3. Database user has appropriate permissions\n" +
            "4. If this is first startup, ensure the database has been initialized\n" +
            "5. If Client and Server start simultaneously, one may fail to seed - this is normal and can be ignored");

        // Log additional diagnostic information
        logger.LogWarning("Application will attempt to start despite seeding failure. Database may have been seeded by another instance. Some features may not work correctly.");

        // Allow the application to continue even if seeding fails
        // This prevents immediate crash and allows users to see error messages in the UI
        // Critical database issues will be caught when users try to access features
        // When Client and Server start together, one might fail to seed due to database locks - this is expected
    }
}

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
app.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
}).RequireAuthorization();

app.MapPost("/account/login", async (
    HttpContext context,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) =>
{
    var form = await context.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var rememberMe = form["rememberMe"].ToString() == "true";

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/login?error=invalid");
    }

    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        return Results.Redirect("/login?error=invalid");
    }

    var result = await signInManager.PasswordSignInAsync(
        user.UserName!,
        password,
        rememberMe,
        lockoutOnFailure: true
    );

    if (result.Succeeded)
    {
        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        return Results.Redirect("/");
    }
    else if (result.IsLockedOut)
    {
        return Results.Redirect("/login?error=locked");
    }
    else if (result.RequiresTwoFactor)
    {
        return Results.Redirect("/login?error=2fa");
    }
    else
    {
        return Results.Redirect("/login?error=invalid");
    }
}).AllowAnonymous();

app.MapPost("/account/register", async (
    HttpContext context,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) =>
{
    var form = await context.Request.ReadFormAsync();
    var firstName = form["firstName"].ToString();
    var lastName = form["lastName"].ToString();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var confirmPassword = form["confirmPassword"].ToString();

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
    {
        return Results.Redirect("/register?error=required");
    }

    if (password != confirmPassword)
    {
        return Results.Redirect("/register?error=mismatch");
    }

    // Check if user already exists
    var existingUser = await userManager.FindByEmailAsync(email);
    if (existingUser != null)
    {
        return Results.Redirect("/register?error=exists");
    }

    var user = new ApplicationUser
    {
        UserName = email,
        Email = email,
        FirstName = firstName,
        LastName = lastName,
        CreatedAt = DateTime.UtcNow,
        IsActive = true
    };

    var result = await userManager.CreateAsync(user, password);

    if (result.Succeeded)
    {
        // Sign in the user
        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Redirect("/?registered=true");
    }
    else
    {
        // Get the first error code, or use a generic error if none found
        var error = result.Errors.FirstOrDefault()?.Code ?? "RegistrationFailed";
        return Results.Redirect($"/register?error={error}");
    }
}).AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
