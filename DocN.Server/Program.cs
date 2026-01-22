using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using DocN.Data;
using DocN.Data.Services;
using DocN.Data.Services.Agents;
using DocN.Server.Services;
using DocN.Server.Services.HealthChecks;
using DocN.Server.Middleware;
using DocN.Core.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;
using Serilog.Events;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters.Prometheus;
using Hangfire;
using Hangfire.SqlServer;
using Hangfire.Console;

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
      ""Microsoft.AspNetCore"": ""Warning"",
      ""Microsoft.EntityFrameworkCore"": ""Information""
    }
  },
  ""AllowedHosts"": ""*"",
  ""ConnectionStrings"": {
    ""DefaultConnection"": ""Server=localhost;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True"",
    ""DocArc"": ""Server=localhost;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True""
  },
  ""Urls"": ""https://localhost:5211;http://localhost:5210""
}";
                File.WriteAllText(appsettingsPath, minimalConfig);
                Console.WriteLine($"Created minimal {appsettingsPath}. Please update with your database connection string.");
            }
        }
        catch (IOException ex)
        {
            // File might be being created by another process (e.g., Client starting at same time)
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
      ""Microsoft.AspNetCore"": ""Warning"",
      ""Microsoft.EntityFrameworkCore"": ""Information""
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

// Ensure configuration files exist
EnsureConfigurationFiles();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/docn-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting DocN Server...");

var builder = WebApplication.CreateBuilder(args);

// Add Serilog to builder
builder.Host.UseSerilog();

// Configure App.Metrics for business and technical metrics
builder.Host.UseMetrics(options =>
{
    options.EndpointOptions = endpointsOptions =>
    {
        endpointsOptions.MetricsTextEndpointOutputFormatter = new MetricsPrometheusTextOutputFormatter();
        endpointsOptions.MetricsEndpointOutputFormatter = new MetricsPrometheusTextOutputFormatter();
    };
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization to handle circular references
        // This prevents errors when serializing entities with navigation properties
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Add HttpClient for IHttpClientFactory with extended timeout for AI operations
builder.Services.AddHttpClient();

// Configure named HttpClient for AI/Gemini operations with extended timeout
builder.Services.AddHttpClient("AI", client =>
{
    // Extended timeout for AI operations (5 minutes)
    // Gemini and other AI providers can take longer to respond during high load
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Configure named HttpClient for general API calls with standard timeout
builder.Services.AddHttpClient("API", client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "DocN API",
        Version = "v1",
        Description = "API REST per la gestione di documenti con RAG (Retrieval-Augmented Generation) e ricerca semantica",
        Contact = new Microsoft.OpenApi.OpenApiContact
        {
            Name = "DocN Support",
            Email = "api-support@docn.example.com"
        }
    });

    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add memory cache for caching service
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024 * 1024 * 100; // 100MB cache limit
});

// Add HttpContextAccessor for audit logging
builder.Services.AddHttpContextAccessor();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5210", "https://localhost:5211", "http://localhost:5036", "https://localhost:7114")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Rate Limiting for API protection
builder.Services.AddRateLimiter(options =>
{
    // Fixed window limiter for general API endpoints
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
    
    // Sliding window for document uploads
    options.AddSlidingWindowLimiter("upload", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(15);
        opt.PermitLimit = 20;
        opt.SegmentsPerWindow = 3;
    });
    
    // Concurrency limiter for AI operations
    options.AddConcurrencyLimiter("ai", opt =>
    {
        opt.PermitLimit = 20;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 50;
    });
    
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        
        double? retryAfterSeconds = null;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = retryAfter.TotalSeconds;
        }
        
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests. Please try again later.",
            retryAfter = retryAfterSeconds
        }, cancellationToken: token);
    };
});

// Configure DbContext
builder.Services.AddDbContext<DocArcContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DocArc");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            // Set command timeout to 30 seconds to prevent long-running queries from hanging
            sqlOptions.CommandTimeout(30);
            // Enable retry on failure for transient errors
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
    }
    else
    {
        // Use in-memory database for development if no connection string is provided
        options.UseInMemoryDatabase("DocArc");
    }
});

// Also register ApplicationDbContext for the new services
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                        ?? builder.Configuration.GetConnectionString("DocArc");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            // Set command timeout to 30 seconds to prevent long-running queries from hanging
            sqlOptions.CommandTimeout(30);
            // Enable retry on failure for transient errors
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
    }
    else
    {
        options.UseInMemoryDatabase("DocArc");
    }
});

// ════════════════════════════════════════════════════════════════════════════════
// Semantic Kernel Configuration - LOADED FROM DATABASE ONLY
// ════════════════════════════════════════════════════════════════════════════════
// The Semantic Kernel is now configured using ONLY database configuration,
// not from appsettings.json. This ensures all AI providers are managed
// centrally through the application's configuration interface.
//
// The SemanticKernelFactory loads the active configuration from the database
// (AIConfigurations table) and creates the Kernel dynamically.
//
// Services that need the Kernel should use IKernelProvider.GetKernelAsync()
// to obtain an instance configured from the database.
// ════════════════════════════════════════════════════════════════════════════════
// Note: Both must be Scoped since they transitively depend on ApplicationDbContext (Scoped)
// ISemanticKernelFactory → IMultiProviderAIService → ApplicationDbContext
builder.Services.AddScoped<ISemanticKernelFactory, SemanticKernelFactory>();
builder.Services.AddScoped<IKernelProvider, KernelProvider>();

// Configure OpenTelemetry for distributed tracing
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "DocN.Server", serviceVersion: "2.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.RecordException = true;
        })
        .AddSource("DocN.*")
        .AddConsoleExporter()
        .AddOtlpExporter(options =>
        {
            // Configure OTLP endpoint if available (e.g., Jaeger, Zipkin)
            var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                options.Endpoint = new Uri(otlpEndpoint);
            }
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("DocN.*")
        .AddConsoleExporter()
        .AddPrometheusExporter());

// Configure Hangfire for background job processing
var hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                            ?? builder.Configuration.GetConnectionString("DocArc");
if (!string.IsNullOrEmpty(hangfireConnectionString))
{
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(hangfireConnectionString, new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        })
        .UseConsole());
    
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = Environment.ProcessorCount * 2;
        options.Queues = new[] { "critical", "default", "low" };
    });
}

// Configure Redis distributed cache (optional, falls back to memory cache if not configured)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "DocN:";
    });
    Log.Information("Redis distributed cache configured");
}
else
{
    Log.Information("Redis not configured, using in-memory cache");
}

// Register core services - use Data layer implementations
builder.Services.AddScoped<DocN.Data.Services.IEmbeddingService, DocN.Data.Services.EmbeddingService>();
builder.Services.AddScoped<DocN.Data.Services.IChunkingService, DocN.Data.Services.ChunkingService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IHybridSearchService, HybridSearchService>();
builder.Services.AddScoped<IBatchProcessingService, BatchProcessingService>();
builder.Services.AddScoped<ILogService, LogService>();

// Register Distributed Cache Service (works with both Redis and in-memory cache)
builder.Services.AddSingleton<IDistributedCacheService, DistributedCacheService>();

// Register Multi-Provider AI Service (supports Gemini, OpenAI, Azure OpenAI from database config)
builder.Services.AddScoped<IMultiProviderAIService, MultiProviderAIService>();

// Register Audit Service for GDPR/SOC2 compliance
builder.Services.AddScoped<IAuditService, AuditService>();

// Register Alerting and Monitoring Services
builder.Services.AddScoped<IAlertingService, AlertingService>();
builder.Services.AddScoped<IRAGQualityService, RAGQualityService>();
builder.Services.AddScoped<IRAGASMetricsService, RAGASMetricsService>();
builder.Services.AddScoped<IGoldenDatasetService, GoldenDatasetService>();

// Register Retrieval and Embedding Enhancement Services
builder.Services.AddScoped<ISemanticChunkingService, SemanticChunkingService>();
builder.Services.AddScoped<IRetrievalMetricsService, RetrievalMetricsService>();
builder.Services.AddScoped<IEmbeddingFineTuningService, EmbeddingFineTuningService>();

// Register Connector and Ingestion Services
builder.Services.AddScoped<IConnectorService, ConnectorService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IIngestionSchedulerHelper, IngestionSchedulerHelper>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentWorkflowService, DocumentWorkflowService>();

// Configure batch processing settings
builder.Services.Configure<DocN.Data.Configuration.BatchProcessingConfiguration>(
    builder.Configuration.GetSection("BatchProcessing"));

// Configure AlertManager settings
builder.Services.Configure<DocN.Core.AI.Configuration.AlertManagerConfiguration>(
    builder.Configuration.GetSection("AlertManager"));

// Configure Enhanced RAG settings
builder.Services.Configure<DocN.Core.AI.Configuration.EnhancedRAGConfiguration>(
    builder.Configuration.GetSection("EnhancedRAG"));

// Configure Contextual Compression settings
builder.Services.Configure<DocN.Core.Interfaces.ContextualCompressionConfiguration>(
    builder.Configuration.GetSection("EnhancedRAG:ContextualCompression"));

// ════════════════════════════════════════════════════════════════════════════════
// RAG Provider Registration - Inizializzazione automatica via Dependency Injection
// ════════════════════════════════════════════════════════════════════════════════
// Il provider RAG viene inizializzato automaticamente dal framework.
// Configurazione: Database AIConfigurations (priorità) o appsettings.json (fallback)
// 
// Feature Flag: EnhancedRAG:UseEnhancedAgentRAG
//   - true: Usa EnhancedAgentRAGService con Microsoft Agent Framework
//   - false: Usa MultiProviderSemanticRAGService (attuale)
// 
// Per dettagli: Vedi docs/MICROSOFT_AGENT_FRAMEWORK_GUIDE.md e docs/QUICK_START_ENHANCED_RAG.md
// ════════════════════════════════════════════════════════════════════════════════

var useEnhancedAgentRAG = builder.Configuration.GetValue<bool>("EnhancedRAG:UseEnhancedAgentRAG", false);
if (useEnhancedAgentRAG)
{
    Log.Information("Using EnhancedAgentRAGService with Microsoft Agent Framework");
    builder.Services.AddScoped<ISemanticRAGService, EnhancedAgentRAGService>();
}
else
{
    Log.Information("Using MultiProviderSemanticRAGService (default)");
    builder.Services.AddScoped<ISemanticRAGService, MultiProviderSemanticRAGService>();
}

// Register agents (used by both implementations if needed)
builder.Services.AddScoped<IRetrievalAgent, RetrievalAgent>();
builder.Services.AddScoped<ISynthesisAgent, SynthesisAgent>();
builder.Services.AddScoped<IClassificationAgent, ClassificationAgent>();
builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

// Register Query Intent Classification and Statistical Answer services
builder.Services.AddScoped<IQueryIntentClassifier, QueryIntentClassifier>();
builder.Services.AddScoped<IDocumentStatisticsService, DocumentStatisticsService>();
builder.Services.AddScoped<IStatisticalAnswerGenerator, StatisticalAnswerGenerator>();

// Register background services
builder.Services.AddHostedService<BatchEmbeddingProcessor>();
builder.Services.AddHostedService<IngestionSchedulerService>();

// Register DatabaseSeeder
builder.Services.AddScoped<DatabaseSeeder>();

// Add Health Checks for monitoring and orchestration
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", tags: new[] { "ready", "db" })
    .AddCheck<AIProviderHealthCheck>("ai_provider", tags: new[] { "ready", "ai" })
    .AddCheck<OCRServiceHealthCheck>("ocr_service", tags: new[] { "ready", "ocr" })
    .AddCheck<SemanticKernelHealthCheck>("semantic_kernel", tags: new[] { "ready", "orchestration" })
    .AddCheck<FileStorageHealthCheck>("file_storage", tags: new[] { "ready", "storage" });

// Add Redis health check if configured
if (!string.IsNullOrEmpty(redisConnectionString))
{
    healthChecksBuilder.AddRedis(redisConnectionString, "redis_cache", tags: new[] { "ready", "cache" });
}

var app = builder.Build();

// Apply pending migrations automatically
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Applying {Count} pending database migrations...", pendingMigrations.Count());
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully");
        }
        else
        {
            logger.LogInformation("Database is up to date - no pending migrations");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations");
        // Continue application startup even if migration fails
        // This allows manual migration via SQL scripts if needed
    }
}

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
        
        logger.LogInformation("Database seeding completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database. The application will continue but may not function correctly without initial data.\n" +
            "Please verify:\n" +
            "1. Database connection string is correct and database server is accessible\n" +
            "2. Database has been created and migrations have been applied\n" +
            "3. Database user has appropriate permissions\n" +
            "4. If Client and Server start simultaneously, one may fail to seed - this is normal and can be ignored");
        
        // Log additional diagnostic information
        logger.LogWarning("Application will attempt to start despite seeding failure. Database may have been seeded by another instance.");
        
        // Allow the application to continue even if seeding fails
        // This is especially important when Client and Server start together
        // as they might conflict when trying to seed the same database
    }
}

// Configure the HTTP request pipeline.
// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "DocN API v1");
    options.RoutePrefix = "swagger"; // Swagger UI at /swagger
    options.DocumentTitle = "DocN API Documentation";
    options.DisplayRequestDuration();
});

// Add Security Headers Middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

// Add Alert Metrics Middleware for monitoring
app.UseMiddleware<AlertMetricsMiddleware>();

// Add Rate Limiting
app.UseRateLimiter();

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();

// Add Hangfire Dashboard (if configured)
if (!string.IsNullOrEmpty(hangfireConnectionString))
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        DashboardTitle = "DocN Background Jobs",
        StatsPollingInterval = 5000, // 5 seconds
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
    Log.Information("Hangfire dashboard available at /hangfire");
}

app.MapControllers();

// Add Prometheus-compatible metrics endpoint via OpenTelemetry
app.UseOpenTelemetryPrometheusScrapingEndpoint();
Log.Information("OpenTelemetry Prometheus metrics endpoint available at /metrics");

// Add custom metrics endpoint for alert system
app.MapGet("/api/metrics/alerts", () =>
{
    return Results.Ok(AlertMetricsMiddleware.GetMetrics());
}).WithTags("Monitoring");

// Add comprehensive health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

// Liveness probe - just checks if the app is running
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // No checks, just returns healthy if app is running
});

// Readiness probe - checks if app is ready to serve requests
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

Log.Information("DocN Server started successfully");
app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
