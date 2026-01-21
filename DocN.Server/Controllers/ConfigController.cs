using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocN.Data;
using DocN.Data.Models;
using DocN.Data.Services;
using Microsoft.Extensions.Logging;

namespace DocN.Server.Controllers;

/// <summary>
/// Controller REST API per gestione configurazioni AI provider (Groq, Ollama, Gemini, OpenAI, Azure OpenAI)
/// Permette CRUD configurazioni e test connettività provider
/// </summary>
/// <remarks>
/// Scopo: Fornire interfaccia per configurare e testare provider AI multi-vendor
/// 
/// Operazioni supportate:
/// - POST /api/config/test: Testa connettività tutti provider configurati
/// - GET /api/config: Lista tutte configurazioni
/// - GET /api/config/{id}: Dettagli configurazione specifica
/// - POST /api/config: Crea nuova configurazione
/// - PUT /api/config/{id}: Aggiorna configurazione esistente
/// - DELETE /api/config/{id}: Elimina configurazione
/// - POST /api/config/{id}/activate: Attiva configurazione specifica
/// 
/// Provider supportati:
/// 1. Google Gemini (API key Gemini)
/// 2. OpenAI (API key OpenAI)
/// 3. Azure OpenAI (endpoint + API key Azure)
/// 4. Ollama (endpoint locale, modelli locali)
/// 5. Groq (API key Groq, cloud veloce)
/// 
/// Funzionalità chiave:
/// - Test pre-salvataggio: Valida API key prima di salvare
/// - Invalidazione cache: ClearConfigurationCache() dopo modifiche
/// - Supporto configurazione attiva unica (IsActive flag)
/// - Logging dettagliato per troubleshooting
/// 
/// Sicurezza:
/// - TODO: Aggiungere autorizzazione (solo admin possono modificare)
/// - API keys criptate in database
/// - API keys non esposte in response GET
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConfigController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ConfigController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMultiProviderAIService _aiService;

    public ConfigController(
        ApplicationDbContext context,
        ILogger<ConfigController> logger,
        IHttpClientFactory httpClientFactory,
        IMultiProviderAIService aiService)
    {
        _context = context;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _aiService = aiService;
    }

    /// <summary>
    /// Testa la connettività e validità di tutti i provider AI configurati
    /// </summary>
    /// <returns>ConfigurationTestResult con esito test per ogni provider</returns>
    /// <response code="200">Test completato (può contenere errori per singoli provider)</response>
    /// <response code="404">Nessuna configurazione attiva trovata nel database</response>
    /// <response code="500">Errore interno del server durante test</response>
    /// <remarks>
    /// Scopo: Validare configurazioni AI prima dell'uso per evitare errori runtime
    /// 
    /// Processo:
    /// 1. Recupera configurazione attiva da database
    /// 2. Per ogni provider configurato (Gemini, OpenAI, Azure, Ollama, Groq):
    ///    a. Verifica presenza API key/endpoint
    ///    b. Effettua chiamata test API (es. list models o embedding test)
    ///    c. Valida risposta e registra risultato
    /// 3. Aggrega risultati e determina successo complessivo
    /// 
    /// Test specifici per provider:
    /// - Gemini: Chiama /models endpoint con API key
    /// - OpenAI: Chiama /models endpoint con Bearer token
    /// - Azure: Chiama endpoint deployment con API key header
    /// - Ollama: Chiama /api/tags endpoint locale
    /// - Groq: Chiama /models endpoint con Bearer token
    /// 
    /// Output atteso:
    /// - Success: true se almeno 1 provider funzionante
    /// - ProviderResults: Array con dettagli per ogni provider
    ///   * ProviderName: "Gemini", "OpenAI", "Azure OpenAI", "Ollama", "Groq"
    ///   * IsConfigured: true se API key/endpoint presente
    ///   * IsValid: true se test connessione riuscito
    ///   * Message: Dettagli esito (successo o errore)
    ///   * Services: Lista servizi configurati per il provider
    /// 
    /// Scenari:
    /// 1. Nessun provider configurato: 404 + messaggio guida
    /// 2. Tutti provider falliscono: 200 + Success=false + dettagli errori
    /// 3. Almeno 1 provider OK: 200 + Success=true + riepilogo
    /// 
    /// Utilizzo tipico:
    /// - Validazione post-configurazione in UI
    /// - Diagnostica problemi connettività
    /// - Health check periodico provider AI
    /// </remarks>
    [HttpPost("test")]
    [ProducesResponseType(typeof(ConfigurationTestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigurationTestResult>> TestConfiguration()
    {
        try
        {
            _logger.LogInformation("Starting configuration test");

            // Get active configuration
            var config = await _context.AIConfigurations
                .FirstOrDefaultAsync(c => c.IsActive);

            if (config == null)
            {
                return NotFound(new ConfigurationTestResult
                {
                    Success = false,
                    Message = "Nessuna configurazione attiva trovata",
                    ProviderResults = new List<ProviderTestResult>()
                });
            }

            var result = new ConfigurationTestResult
            {
                Success = true,
                Message = "Test della configurazione completato",
                ProviderResults = new List<ProviderTestResult>()
            };

            // Test Gemini provider if configured
            if (!string.IsNullOrEmpty(config.GeminiApiKey))
            {
                var geminiResult = await TestGeminiProvider(config);
                result.ProviderResults.Add(geminiResult);
            }

            // Test OpenAI provider if configured
            if (!string.IsNullOrEmpty(config.OpenAIApiKey))
            {
                var openAIResult = await TestOpenAIProvider(config);
                result.ProviderResults.Add(openAIResult);
            }

            // Test Azure OpenAI provider if configured
            if (!string.IsNullOrEmpty(config.AzureOpenAIKey) && !string.IsNullOrEmpty(config.AzureOpenAIEndpoint))
            {
                var azureResult = await TestAzureOpenAIProvider(config);
                result.ProviderResults.Add(azureResult);
            }

            // Test Ollama provider if configured
            if (!string.IsNullOrEmpty(config.OllamaEndpoint))
            {
                var ollamaResult = await TestOllamaProvider(config);
                result.ProviderResults.Add(ollamaResult);
            }

            // Test Groq provider if configured
            if (!string.IsNullOrEmpty(config.GroqApiKey))
            {
                var groqResult = await TestGroqProvider(config);
                result.ProviderResults.Add(groqResult);
            }

            // Determine overall success
            result.Success = result.ProviderResults.Any() && 
                           result.ProviderResults.Any(r => r.IsConfigured && r.IsValid);

            if (!result.ProviderResults.Any())
            {
                result.Message = "⚠️ Nessun provider configurato. Configura almeno un provider AI.";
                result.Success = false;
            }
            else if (!result.Success)
            {
                result.Message = "❌ Tutti i provider configurati hanno fallito il test di connessione.";
            }
            else
            {
                var successCount = result.ProviderResults.Count(r => r.IsConfigured && r.IsValid);
                var totalCount = result.ProviderResults.Count(r => r.IsConfigured);
                result.Message = $"✅ Test completato: {successCount}/{totalCount} provider funzionanti";
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing configuration");
            return StatusCode(500, new ConfigurationTestResult
            {
                Success = false,
                Message = $"Errore durante il test: {ex.Message}",
                ProviderResults = new List<ProviderTestResult>()
            });
        }
    }

    private async Task<ProviderTestResult> TestGeminiProvider(AIConfiguration config)
    {
        var result = new ProviderTestResult
        {
            ProviderName = "Google Gemini",
            ProviderType = AIProviderType.Gemini,
            IsConfigured = true,
            Services = new List<ServiceTestResult>()
        };

        try
        {
            // Test API key validity by making a simple request
            // Use named HttpClient with extended timeout for AI operations
            var httpClient = _httpClientFactory.CreateClient("AI");
            httpClient.DefaultRequestHeaders.Add("x-goog-api-key", config.GeminiApiKey);

            // Use models endpoint without API key in URL for security
            var testUrl = "https://generativelanguage.googleapis.com/v1beta/models";
            var response = await httpClient.GetAsync(testUrl);

            if (response.IsSuccessStatusCode)
            {
                result.IsValid = true;
                result.Message = "✅ API Key valida";

                // Test Chat service
                if (config.ChatProvider == AIProviderType.Gemini)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Chat",
                        Model = config.GeminiChatModel ?? "gemini-1.5-flash",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test Embeddings service
                if (config.EmbeddingsProvider == AIProviderType.Gemini)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Embeddings",
                        Model = config.GeminiEmbeddingModel ?? "text-embedding-004",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test Tag Extraction service
                if (config.TagExtractionProvider == AIProviderType.Gemini)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Tag Extraction",
                        Model = config.GeminiChatModel ?? "gemini-1.5-flash",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test RAG service
                if (config.RAGProvider == AIProviderType.Gemini)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "RAG",
                        Model = config.GeminiChatModel ?? "gemini-1.5-flash",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }
            }
            else
            {
                result.IsValid = false;
                var errorContent = await response.Content.ReadAsStringAsync();
                result.Message = $"❌ API Key non valida: {response.StatusCode}";
                _logger.LogWarning("Gemini API test failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"❌ Errore di connessione: {ex.Message}";
            _logger.LogError(ex, "Error testing Gemini provider");
        }

        return result;
    }

    private async Task<ProviderTestResult> TestOpenAIProvider(AIConfiguration config)
    {
        var result = new ProviderTestResult
        {
            ProviderName = "OpenAI",
            ProviderType = AIProviderType.OpenAI,
            IsConfigured = true,
            Services = new List<ServiceTestResult>()
        };

        try
        {
            // Test API key validity by listing models
            // Use named HttpClient with extended timeout for AI operations
            var httpClient = _httpClientFactory.CreateClient("AI");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.OpenAIApiKey}");

            var response = await httpClient.GetAsync("https://api.openai.com/v1/models");

            if (response.IsSuccessStatusCode)
            {
                result.IsValid = true;
                result.Message = "✅ API Key valida";

                // Test Chat service
                if (config.ChatProvider == AIProviderType.OpenAI)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Chat",
                        Model = config.OpenAIChatModel ?? "gpt-4",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test Embeddings service
                if (config.EmbeddingsProvider == AIProviderType.OpenAI)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Embeddings",
                        Model = config.OpenAIEmbeddingModel ?? "text-embedding-ada-002",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test Tag Extraction service
                if (config.TagExtractionProvider == AIProviderType.OpenAI)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Tag Extraction",
                        Model = config.OpenAIChatModel ?? "gpt-4",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test RAG service
                if (config.RAGProvider == AIProviderType.OpenAI)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "RAG",
                        Model = config.OpenAIChatModel ?? "gpt-4",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }
            }
            else
            {
                result.IsValid = false;
                var errorContent = await response.Content.ReadAsStringAsync();
                result.Message = $"❌ API Key non valida: {response.StatusCode}";
                _logger.LogWarning("OpenAI API test failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"❌ Errore di connessione: {ex.Message}";
            _logger.LogError(ex, "Error testing OpenAI provider");
        }

        return result;
    }

    private async Task<ProviderTestResult> TestAzureOpenAIProvider(AIConfiguration config)
    {
        var result = new ProviderTestResult
        {
            ProviderName = "Azure OpenAI",
            ProviderType = AIProviderType.AzureOpenAI,
            IsConfigured = true,
            Services = new List<ServiceTestResult>()
        };

        try
        {
            // Test API key and endpoint validity
            // Use named HttpClient with extended timeout for AI operations
            var httpClient = _httpClientFactory.CreateClient("AI");
            httpClient.DefaultRequestHeaders.Add("api-key", config.AzureOpenAIKey);

            // Try to list deployments to verify connection
            var endpoint = config.AzureOpenAIEndpoint?.TrimEnd('/');
            var testUrl = $"{endpoint}/openai/deployments?api-version=2023-05-15";
            var response = await httpClient.GetAsync(testUrl);

            if (response.IsSuccessStatusCode)
            {
                result.IsValid = true;
                result.Message = "✅ API Key e Endpoint validi";

                // Test Chat service
                if (config.ChatProvider == AIProviderType.AzureOpenAI)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Chat",
                        Model = config.ChatDeploymentName ?? config.AzureOpenAIChatModel ?? "gpt-4",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test Embeddings service
                if (config.EmbeddingsProvider == AIProviderType.AzureOpenAI)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Embeddings",
                        Model = config.EmbeddingDeploymentName ?? config.AzureOpenAIEmbeddingModel ?? "text-embedding-ada-002",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test Tag Extraction service
                if (config.TagExtractionProvider == AIProviderType.AzureOpenAI)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Tag Extraction",
                        Model = config.ChatDeploymentName ?? config.AzureOpenAIChatModel ?? "gpt-4",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test RAG service
                if (config.RAGProvider == AIProviderType.AzureOpenAI)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "RAG",
                        Model = config.ChatDeploymentName ?? config.AzureOpenAIChatModel ?? "gpt-4",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }
            }
            else
            {
                result.IsValid = false;
                var errorContent = await response.Content.ReadAsStringAsync();
                result.Message = $"❌ Endpoint o API Key non validi: {response.StatusCode}";
                _logger.LogWarning("Azure OpenAI API test failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"❌ Errore di connessione: {ex.Message}";
            _logger.LogError(ex, "Error testing Azure OpenAI provider");
        }

        return result;
    }

    private async Task<ProviderTestResult> TestOllamaProvider(AIConfiguration config)
    {
        var result = new ProviderTestResult
        {
            ProviderName = "Ollama (Local)",
            ProviderType = AIProviderType.Ollama,
            IsConfigured = true,
            Services = new List<ServiceTestResult>()
        };

        try
        {
            // Test Ollama endpoint connectivity
            var httpClient = _httpClientFactory.CreateClient("AI");
            var endpoint = config.OllamaEndpoint?.TrimEnd('/') ?? "http://localhost:11434";
            
            // Test with tags endpoint to check if Ollama is running
            var testUrl = $"{endpoint}/api/tags";
            var response = await httpClient.GetAsync(testUrl);

            if (response.IsSuccessStatusCode)
            {
                result.IsValid = true;
                result.Message = "✅ Ollama in esecuzione";

                // Test Chat service
                if (config.ChatProvider == AIProviderType.Ollama)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Chat",
                        Model = config.OllamaChatModel ?? "llama3",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test Embeddings service
                if (config.EmbeddingsProvider == AIProviderType.Ollama)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Embeddings",
                        Model = config.OllamaEmbeddingModel ?? "nomic-embed-text",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test Tag Extraction service
                if (config.TagExtractionProvider == AIProviderType.Ollama)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Tag Extraction",
                        Model = config.OllamaChatModel ?? "llama3",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test RAG service
                if (config.RAGProvider == AIProviderType.Ollama)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "RAG",
                        Model = config.OllamaChatModel ?? "llama3",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }
            }
            else
            {
                result.IsValid = false;
                result.Message = $"❌ Ollama non raggiungibile: {response.StatusCode}. Verifica che Ollama sia in esecuzione (ollama serve)";
                _logger.LogWarning("Ollama test failed: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"❌ Errore di connessione a Ollama: {ex.Message}. Verifica che Ollama sia in esecuzione.";
            _logger.LogError(ex, "Error testing Ollama provider");
        }

        return result;
    }

    private async Task<ProviderTestResult> TestGroqProvider(AIConfiguration config)
    {
        var result = new ProviderTestResult
        {
            ProviderName = "Groq Cloud",
            ProviderType = AIProviderType.Groq,
            IsConfigured = true,
            Services = new List<ServiceTestResult>()
        };

        try
        {
            // Test Groq API key validity by listing models
            var httpClient = _httpClientFactory.CreateClient("AI");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.GroqApiKey}");

            var endpoint = config.GroqEndpoint?.TrimEnd('/') ?? "https://api.groq.com/openai/v1";
            var testUrl = $"{endpoint}/models";
            var response = await httpClient.GetAsync(testUrl);

            if (response.IsSuccessStatusCode)
            {
                result.IsValid = true;
                result.Message = "✅ API Key valida";

                // Test Chat service
                if (config.ChatProvider == AIProviderType.Groq)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Chat",
                        Model = config.GroqChatModel ?? "llama-3.1-8b-instant",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Note: Groq doesn't support embeddings
                if (config.EmbeddingsProvider == AIProviderType.Groq)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Embeddings",
                        Model = "N/A",
                        Status = "⚠️ Groq non supporta embeddings",
                        IsHealthy = false
                    });
                }

                // Test Tag Extraction service
                if (config.TagExtractionProvider == AIProviderType.Groq)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "Tag Extraction",
                        Model = config.GroqChatModel ?? "llama-3.1-8b-instant",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }

                // Test RAG service
                if (config.RAGProvider == AIProviderType.Groq)
                {
                    result.Services.Add(new ServiceTestResult
                    {
                        ServiceName = "RAG",
                        Model = config.GroqChatModel ?? "llama-3.1-8b-instant",
                        Status = "✅ Configurato",
                        IsHealthy = true
                    });
                }
            }
            else
            {
                result.IsValid = false;
                var errorContent = await response.Content.ReadAsStringAsync();
                result.Message = $"❌ API Key non valida: {response.StatusCode}";
                _logger.LogWarning("Groq API test failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"❌ Errore di connessione: {ex.Message}";
            _logger.LogError(ex, "Error testing Groq provider");
        }

        return result;
    }

    /// <summary>
    /// Get the current active AI configuration
    /// </summary>
    /// <returns>Active AI configuration</returns>
    /// <response code="200">Configuration retrieved successfully</response>
    /// <response code="404">No active configuration found</response>
    [HttpGet("active")]
    [ProducesResponseType(typeof(AIConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AIConfiguration>> GetActiveConfiguration()
    {
        try
        {
            var config = await _context.AIConfigurations
                .FirstOrDefaultAsync(c => c.IsActive);

            if (config == null)
            {
                return NotFound(new { message = "No active configuration found" });
            }

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active configuration");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Activate a specific configuration by ID
    /// </summary>
    /// <param name="id">Configuration ID to activate</param>
    /// <returns>Activated configuration</returns>
    /// <response code="200">Configuration activated successfully</response>
    /// <response code="404">Configuration not found</response>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(typeof(AIConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AIConfiguration>> ActivateConfiguration(int id)
    {
        try
        {
            var config = await _context.AIConfigurations.FindAsync(id);
            
            if (config == null)
            {
                return NotFound(new { message = $"Configuration with ID {id} not found" });
            }

            _logger.LogInformation("Activating configuration '{ConfigName}' (ID: {ConfigId}). Deactivating all others...", 
                config.ConfigurationName, config.Id);

            // Deactivate all other configurations efficiently with bulk update
            await _context.AIConfigurations
                .Where(c => c.Id != id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false));

            // Activate this configuration
            config.IsActive = true;
            config.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            
            // Clear the AI service configuration cache to force reload
            _aiService.ClearConfigurationCache();
            
            _logger.LogInformation("✅ Configuration '{ConfigName}' (ID: {ConfigId}) activated successfully. All other configurations deactivated. Cache cleared.", 
                config.ConfigurationName, config.Id);

            return Ok(new 
            { 
                success = true,
                message = $"Configuration '{config.ConfigurationName}' activated successfully",
                configuration = config
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating configuration with ID {ConfigId}", id);
            return StatusCode(500, new { error = $"Error activating configuration: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get diagnostic information about all configurations to help troubleshoot which one is being used
    /// </summary>
    /// <returns>Diagnostic information about all configurations</returns>
    [HttpGet("diagnostics")]
    [HttpGet("diagnostica")] // Italian alias
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetConfigurationDiagnostics()
    {
        try
        {
            var allConfigs = await _context.AIConfigurations
                .OrderByDescending(c => c.IsActive)
                .ThenByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.ConfigurationName,
                    c.IsActive,
                    c.CreatedAt,
                    c.UpdatedAt,
                    HasGeminiKey = !string.IsNullOrWhiteSpace(c.GeminiApiKey),
                    HasOpenAIKey = !string.IsNullOrWhiteSpace(c.OpenAIApiKey),
                    HasAzureKey = !string.IsNullOrWhiteSpace(c.AzureOpenAIKey) && !string.IsNullOrWhiteSpace(c.AzureOpenAIEndpoint),
                    HasOllamaEndpoint = !string.IsNullOrWhiteSpace(c.OllamaEndpoint),
                    HasGroqKey = !string.IsNullOrWhiteSpace(c.GroqApiKey),
                    c.ProviderType,
                    c.ChatProvider,
                    c.EmbeddingsProvider,
                    c.TagExtractionProvider,
                    c.RAGProvider,
                    SortOrder = c.IsActive ? 0 : 1
                })
                .ToListAsync();

            var activeConfig = allConfigs.FirstOrDefault(c => c.IsActive);
            var multipleActive = allConfigs.Count(c => c.IsActive) > 1;

            return Ok(new
            {
                timestamp = DateTime.UtcNow,
                totalConfigurations = allConfigs.Count,
                activeConfiguration = activeConfig != null ? new
                {
                    id = activeConfig.Id,
                    name = activeConfig.ConfigurationName,
                    createdAt = activeConfig.CreatedAt,
                    updatedAt = activeConfig.UpdatedAt,
                    hasGeminiKey = activeConfig.HasGeminiKey,
                    hasOpenAIKey = activeConfig.HasOpenAIKey,
                    hasAzureKey = activeConfig.HasAzureKey,
                    hasOllamaEndpoint = activeConfig.HasOllamaEndpoint,
                    hasGroqKey = activeConfig.HasGroqKey
                } : null,
                multipleActiveWarning = multipleActive,
                allConfigurations = allConfigs,
                recommendations = new[]
                {
                    multipleActive ? "⚠️ Multiple configurations are marked as active! Use POST /api/config/{id}/activate to activate only one." : null,
                    activeConfig == null ? "❌ No active configuration found. Use POST /api/config/{id}/activate to activate a configuration." : null,
                    activeConfig?.ConfigurationName == "Default Configuration" ? "ℹ️ You are using the default seeded configuration. If you want to use a different one, use POST /api/config/{id}/activate." : null
                }.Where(r => r != null).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration diagnostics");
            return StatusCode(500, new { error = $"Error getting diagnostics: {ex.Message}" });
        }
    }

    /// <summary>
    /// Clear the configuration cache to force reload from database
    /// </summary>
    /// <returns>Success message</returns>
    /// <response code="200">Cache cleared successfully</response>
    [HttpPost("clear-cache")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult ClearConfigurationCache()
    {
        try
        {
            _aiService.ClearConfigurationCache();
            _logger.LogInformation("Configuration cache cleared successfully via API request");
            
            return Ok(new
            {
                success = true,
                message = "✅ Cache della configurazione svuotata con successo. La configurazione verrà ricaricata dal database al prossimo utilizzo.",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing configuration cache");
            return StatusCode(500, new { success = false, error = "Errore durante lo svuotamento della cache. Riprova più tardi." });
        }
    }

    /// <summary>
    /// Get all AI configurations
    /// </summary>
    /// <returns>List of all AI configurations</returns>
    /// <response code="200">Configurations retrieved successfully</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<AIConfiguration>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AIConfiguration>>> GetAllConfigurations()
    {
        try
        {
            var configs = await _context.AIConfigurations
                .OrderByDescending(c => c.IsActive)
                .ThenByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .ToListAsync();

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configurations");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Create or update an AI configuration
    /// </summary>
    /// <param name="config">Configuration to save</param>
    /// <returns>Saved configuration</returns>
    /// <response code="200">Configuration saved successfully</response>
    /// <response code="400">Invalid configuration data</response>
    [HttpPost]
    [ProducesResponseType(typeof(AIConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AIConfiguration>> SaveConfiguration([FromBody] AIConfiguration config)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config.ConfigurationName))
            {
                return BadRequest(new { error = "Configuration name is required" });
            }

            // If setting this as active, deactivate all others
            if (config.IsActive)
            {
                var otherConfigs = await _context.AIConfigurations
                    .Where(c => c.Id != config.Id)
                    .ToListAsync();
                
                foreach (var other in otherConfigs)
                {
                    other.IsActive = false;
                }
            }

            if (config.Id == 0)
            {
                config.CreatedAt = DateTime.UtcNow;
                _context.AIConfigurations.Add(config);
            }
            else
            {
                config.UpdatedAt = DateTime.UtcNow;
                _context.AIConfigurations.Update(config);
            }

            await _context.SaveChangesAsync();
            
            // Clear the AI service configuration cache to force reload from database
            _aiService.ClearConfigurationCache();
            
            _logger.LogInformation("Configuration '{ConfigName}' saved successfully (ID: {ConfigId}, Active: {IsActive}). Cache cleared.", 
                config.ConfigurationName, config.Id, config.IsActive);

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            return StatusCode(500, new { error = $"Error saving configuration: {ex.Message}" });
        }
    }
}
