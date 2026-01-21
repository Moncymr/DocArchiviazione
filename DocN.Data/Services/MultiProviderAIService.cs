using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using Azure.AI.OpenAI;
using Azure;
using DocN.Data.Models;
using OpenAI.Embeddings;
using OpenAI.Chat;
using System.IO;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Multi-provider AI service interface for RAG operations
/// Supports Gemini, OpenAI, and Azure OpenAI providers
/// </summary>
public interface IMultiProviderAIService
{
    Task<float[]?> GenerateEmbeddingAsync(string text);
    Task<string> GenerateChatCompletionAsync(string systemPrompt, string userPrompt);
    Task<(string Category, string Reasoning, string Provider)> SuggestCategoryAsync(string fileName, string extractedText);
    Task<List<string>> ExtractTagsAsync(string extractedText);
    Task<Dictionary<string, string>> ExtractMetadataAsync(string extractedText, string fileName = "");
    string GetCurrentChatProvider();
    string GetCurrentEmbeddingProvider();
    Task<AIConfiguration?> GetActiveConfigurationAsync();
    void ClearConfigurationCache();
}

/// <summary>
/// Servizio multi-provider per operazioni AI (chat, embeddings, estrazione metadati)
/// Supporta Google Gemini, OpenAI e Azure OpenAI con fallback automatico
/// </summary>
/// <remarks>
/// Scopo: Fornire astrazione unificata per provider AI multipli con selezione automatica e ridondanza
/// 
/// Funzionalità chiave:
/// - Configurazione esclusivamente da database (nessun fallback ad appsettings.json)
/// - Caching configurazione (5 minuti) per performance
/// - Fallback automatico tra provider in caso di errore
/// - Provider specifici per servizio (Chat, Embeddings, TagExtraction)
/// - Supporto hot-reload configurazione via ClearConfigurationCache()
/// 
/// Provider supportati:
/// 1. Google Gemini (gemini-2.0-flash-exp, text-embedding-004)
/// 2. OpenAI (gpt-4, text-embedding-3-large)
/// 3. Azure OpenAI (deployment custom)
/// 
/// Per dettagli: Vedi RAG_PROVIDER_INITIALIZATION_GUIDE.md
/// </remarks>
public class MultiProviderAIService : IMultiProviderAIService
{
    private readonly IConfiguration _configuration;        // ← Mantenuto per compatibilità ma non usato per configurazione AI
    private readonly ApplicationDbContext _context;        // ← Database AIConfigurations (unica fonte)
    private readonly ILogService _logService;
    private AIConfiguration? _cachedConfig;
    private DateTime _lastConfigCheck = DateTime.MinValue;
    private readonly TimeSpan _configCacheDuration = TimeSpan.FromMinutes(5);
    private readonly int _aiTimeoutSeconds;

    public MultiProviderAIService(IConfiguration configuration, ApplicationDbContext context, ILogService logService)
    {
        _configuration = configuration;
        _context = context;
        _logService = logService;
        
        // Read AI timeout from configuration (default 120 seconds = 2 minutes for faster feedback)
        _aiTimeoutSeconds = configuration.GetValue<int>("AI:TimeoutSeconds", 120);
    }

    /// <summary>
    /// Ottiene la configurazione AI attiva esclusivamente dal database
    /// </summary>
    /// <returns>AIConfiguration attiva o null se nessuna configurazione disponibile</returns>
    /// <remarks>
    /// Scopo: Recuperare configurazione AI con caching per ottimizzare performance
    /// 
    /// Processo:
    /// 1. Verifica cache (valida per 5 minuti)
    /// 2. Se cache scaduta o vuota, interroga database
    /// 3. Cerca configurazione con IsActive=true ordinata per data aggiornamento
    /// 4. Se non trovata nel DB, restituisce null (NESSUN FALLBACK ad appsettings.json)
    /// 5. Aggiorna cache e timestamp ultimo controllo
    /// 
    /// Output atteso:
    /// - AIConfiguration con provider configurati (Gemini/OpenAI/Azure)
    /// - null se nessuna configurazione valida disponibile nel database
    /// 
    /// Cache strategy:
    /// - Durata: 5 minuti (bilanciamento performance vs freschezza)
    /// - Invalidazione manuale: ClearConfigurationCache()
    /// - Auto-refresh: dopo timeout o modifica database
    /// </remarks>
    public async Task<AIConfiguration?> GetActiveConfigurationAsync()
    {
        // Check if cached configuration is still valid
        if (_cachedConfig != null && DateTime.UtcNow - _lastConfigCheck < _configCacheDuration)
        {
            await _logService.LogDebugAsync("Configuration", "Using cached configuration", $"Config: {_cachedConfig.ConfigurationName}, IsActive: {_cachedConfig.IsActive}");
            return _cachedConfig;
        }

        try
        {
            // Priority: Fetch active configuration from database
            await _logService.LogDebugAsync("Configuration", "Fetching active configuration from database...");
            
            // Get all active configurations (there should be only one, but we'll handle multiple)
            var activeConfigs = await _context.AIConfigurations
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .ToListAsync();

            _lastConfigCheck = DateTime.UtcNow;

            // Check if there are multiple active configurations (shouldn't happen, but let's handle it)
            if (activeConfigs.Count > 1)
            {
                await _logService.LogWarningAsync("Configuration", 
                    $"⚠️ Found {activeConfigs.Count} active configurations. Using most recently updated: {activeConfigs.First().ConfigurationName}");
                _cachedConfig = activeConfigs.First();
            }
            else if (activeConfigs.Count == 1)
            {
                _cachedConfig = activeConfigs.First();
            }
            else
            {
                _cachedConfig = null;
            }

            if (_cachedConfig != null)
            {
                await _logService.LogInfoAsync("Configuration", 
                    $"✅ Loaded active configuration from database: {_cachedConfig.ConfigurationName} (ID: {_cachedConfig.Id}, CreatedAt: {_cachedConfig.CreatedAt:yyyy-MM-dd HH:mm:ss}, UpdatedAt: {_cachedConfig.UpdatedAt:yyyy-MM-dd HH:mm:ss})");
                
                // Log which providers are configured
                var configuredProviders = new List<string>();
                
                // Check Gemini - use GeminiApiKey or fall back to ProviderApiKey if ProviderType is Gemini
                var geminiKey = GetEffectiveApiKey(_cachedConfig.GeminiApiKey, AIProviderType.Gemini, _cachedConfig);
                if (!string.IsNullOrWhiteSpace(geminiKey))
                {
                    configuredProviders.Add("Gemini");
                }
                
                // Check OpenAI - use OpenAIApiKey or fall back to ProviderApiKey if ProviderType is OpenAI
                var openAIKey = GetEffectiveApiKey(_cachedConfig.OpenAIApiKey, AIProviderType.OpenAI, _cachedConfig);
                if (!string.IsNullOrWhiteSpace(openAIKey))
                {
                    configuredProviders.Add("OpenAI");
                }
                
                // Check Azure OpenAI - need both endpoint and key
                var azureKey = GetEffectiveApiKey(_cachedConfig.AzureOpenAIKey, AIProviderType.AzureOpenAI, _cachedConfig);
                var azureEndpoint = GetEffectiveEndpoint(_cachedConfig.AzureOpenAIEndpoint, AIProviderType.AzureOpenAI, _cachedConfig);
                if (!string.IsNullOrWhiteSpace(azureKey) && !string.IsNullOrWhiteSpace(azureEndpoint))
                {
                    configuredProviders.Add("Azure OpenAI");
                }
                
                if (configuredProviders.Any())
                {
                    await _logService.LogInfoAsync("Configuration", $"Configured providers: {string.Join(", ", configuredProviders)}");
                }
                else
                {
                    await _logService.LogWarningAsync("Configuration", "⚠️ Configuration found in database but no API keys are set!");
                    // Add minimal debug info to help diagnose the issue
                    await _logService.LogDebugAsync("Configuration", "Provider configuration check",
                        $"ProviderType: {_cachedConfig.ProviderType}, " +
                        $"GeminiApiKey: {(_cachedConfig.GeminiApiKey != null ? "set" : "null")}, " +
                        $"OpenAIApiKey: {(_cachedConfig.OpenAIApiKey != null ? "set" : "null")}, " +
                        $"AzureOpenAIKey: {(_cachedConfig.AzureOpenAIKey != null ? "set" : "null")}, " +
                        $"AzureOpenAIEndpoint: {(_cachedConfig.AzureOpenAIEndpoint != null ? "set" : "null")}, " +
                        $"ProviderApiKey: {(_cachedConfig.ProviderApiKey != null ? "set" : "null")}");
                }
            }
            else
            {
                // NO FALLBACK: Always require database configuration
                await _logService.LogErrorAsync("Configuration", "❌ No active configuration found in database! Please configure AI providers via the application UI or database.");
                _cachedConfig = null;
            }

            return _cachedConfig;
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync("Configuration", "❌ Error loading configuration from database. Cannot proceed without database configuration!", ex.Message, stackTrace: ex.StackTrace);
            
            // NO FALLBACK: Always require database configuration
            _cachedConfig = null;
            _lastConfigCheck = DateTime.UtcNow;
            
            return _cachedConfig;
        }
    }

    /// <summary>
    /// Gets the effective API key for a provider, using provider-specific key or falling back to ProviderApiKey.
    /// </summary>
    /// <param name="providerKey">Provider-specific API key (e.g., GeminiApiKey, OpenAIApiKey)</param>
    /// <param name="targetProviderType">The provider type to check for fallback</param>
    /// <param name="config">AI configuration</param>
    /// <returns>The effective API key or null</returns>
    private string? GetEffectiveApiKey(string? providerKey, AIProviderType targetProviderType, AIConfiguration config)
    {
        return providerKey ?? (config.ProviderType == targetProviderType ? config.ProviderApiKey : null);
    }

    /// <summary>
    /// Gets the effective endpoint for a provider, using provider-specific endpoint or falling back to ProviderEndpoint.
    /// </summary>
    /// <param name="providerEndpoint">Provider-specific endpoint (e.g., AzureOpenAIEndpoint)</param>
    /// <param name="targetProviderType">The provider type to check for fallback</param>
    /// <param name="config">AI configuration</param>
    /// <returns>The effective endpoint or null</returns>
    private string? GetEffectiveEndpoint(string? providerEndpoint, AIProviderType targetProviderType, AIConfiguration config)
    {
        return providerEndpoint ?? (config.ProviderType == targetProviderType ? config.ProviderEndpoint : null);
    }


    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        // Truncate very long text to avoid API limits
        if (text.Length > 10000)
        {
            text = text.Substring(0, 10000);
        }

        try
        {
            return await ExecuteWithTimeoutAsync(async (cancellationToken) =>
            {
                var config = await GetActiveConfigurationAsync();
                if (config == null)
                {
                    throw new InvalidOperationException("Nessuna configurazione AI attiva trovata nel database. Configura un provider AI tramite l'interfaccia utente.");
                }

                // Determine which provider to use for embeddings
                var provider = config.EmbeddingsProvider ?? config.ProviderType;
                
                // Log which provider is being used for embeddings
                await _logService.LogInfoAsync("Embedding", $"Using {provider} for embedding generation");

                // Try primary embedding provider
                try
                {
                    return provider switch
                    {
                        AIProviderType.AzureOpenAI => await GenerateEmbeddingWithAzureOpenAIAsync(text, config),
                        AIProviderType.OpenAI => await GenerateEmbeddingWithOpenAIAsync(text, config),
                        AIProviderType.Gemini => await GenerateEmbeddingWithGeminiAsync(text, config),
                        _ => throw new InvalidOperationException($"Provider non supportato: {provider}")
                    };
                }
                catch (Exception ex)
                {
                    // Log the exception for debugging
                    await _logService.LogErrorAsync("Embedding", $"Provider di embedding primario ({provider}) fallito", ex.Message, stackTrace: ex.StackTrace);
                    
                    // If primary fails and fallback is enabled, try alternatives
                    if (config.EnableFallback)
                    {
                        var errors = new List<string> { $"{provider}: {ex.Message}" };
                        
                        // Try alternative providers, but skip the one that just failed
                        var geminiKey = GetEffectiveApiKey(config.GeminiApiKey, AIProviderType.Gemini, config);
                        if (provider != AIProviderType.Gemini && !string.IsNullOrWhiteSpace(geminiKey))
                        {
                            try
                            {
                                await _logService.LogInfoAsync("Embedding", "Tentativo Gemini come fallback");
                                return await GenerateEmbeddingWithGeminiAsync(text, config);
                            }
                            catch (Exception ex2)
                            {
                                await _logService.LogWarningAsync("Embedding", "Fallback Gemini fallito", ex2.Message);
                                errors.Add($"Gemini: {ex2.Message}");
                            }
                        }
                        
                        var openAIKey = GetEffectiveApiKey(config.OpenAIApiKey, AIProviderType.OpenAI, config);
                        if (provider != AIProviderType.OpenAI && !string.IsNullOrWhiteSpace(openAIKey))
                        {
                            try
                            {
                                await _logService.LogInfoAsync("Embedding", "Tentativo OpenAI come fallback");
                                return await GenerateEmbeddingWithOpenAIAsync(text, config);
                            }
                            catch (Exception ex3)
                            {
                                await _logService.LogWarningAsync("Embedding", "Fallback OpenAI fallito", ex3.Message);
                                errors.Add($"OpenAI: {ex3.Message}");
                            }
                        }
                        
                        var azureKey = GetEffectiveApiKey(config.AzureOpenAIKey, AIProviderType.AzureOpenAI, config);
                        var azureEndpoint = GetEffectiveEndpoint(config.AzureOpenAIEndpoint, AIProviderType.AzureOpenAI, config);
                        if (provider != AIProviderType.AzureOpenAI && !string.IsNullOrWhiteSpace(azureKey) && !string.IsNullOrWhiteSpace(azureEndpoint))
                        {
                            try
                            {
                                await _logService.LogInfoAsync("Embedding", "Tentativo AzureOpenAI come fallback");
                                return await GenerateEmbeddingWithAzureOpenAIAsync(text, config);
                            }
                            catch (Exception ex4)
                            {
                                await _logService.LogWarningAsync("Embedding", "Fallback AzureOpenAI fallito", ex4.Message);
                                errors.Add($"AzureOpenAI: {ex4.Message}");
                            }
                        }
                        
                        // All fallback attempts failed
                        var errorMessage = $"Tutti i provider di embedding sono falliti. Errori: {string.Join("; ", errors)}";
                        
                        // Check if any error indicates a quota issue (formatted messages use 🚫, raw errors have quota keywords)
                        var hasQuotaIssue = errors.Any(e => e.Contains("🚫") || IsQuotaExceededError(e));
                        if (hasQuotaIssue)
                        {
                            errorMessage += "\n\n💡 Suggerimento: Uno o più provider hanno esaurito la quota. " +
                                           "Configura un provider alternativo con quota disponibile o attendi il reset della quota.";
                        }
                        
                        throw new InvalidOperationException(errorMessage);
                    }
                    
                    // Fallback is disabled, throw the original exception
                    throw;
                }
            }, "EmbeddingGeneration");
        }
        catch (TimeoutException tex)
        {
            // Timeout occurred - log and throw with better message
            await _logService.LogWarningAsync("Embedding", "Embedding generation timed out", tex.Message);
            throw new TimeoutException($"⏱️ Timeout: La generazione degli embeddings ha richiesto troppo tempo ({_aiTimeoutSeconds}s). Il provider AI potrebbe essere sovraccarico. Riprova più tardi.", tex);
        }
    }

    private async Task<float[]?> GenerateEmbeddingWithAzureOpenAIAsync(string text, AIConfiguration config)
    {
        var endpoint = config.AzureOpenAIEndpoint ?? config.ProviderEndpoint;
        var apiKey = config.AzureOpenAIKey ?? config.ProviderApiKey;
        var deploymentName = config.EmbeddingDeploymentName ?? config.AzureOpenAIEmbeddingModel ?? "text-embedding-ada-002";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Endpoint e API key di Azure OpenAI non configurati");
        }

        var azureClient = new AzureOpenAIClient(
            new Uri(endpoint), 
            new AzureKeyCredential(apiKey));
        
        var embeddingClient = azureClient.GetEmbeddingClient(deploymentName);
        var response = await embeddingClient.GenerateEmbeddingAsync(text);
        
        var embedding = response.Value.ToFloats().ToArray();
        
        // Log embedding info for debugging
        await _logService.LogDebugAsync("Embedding", $"[AzureOpenAI] Generated embedding: {embedding.Length} dimensions", $"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F6")))}]");
        
        return embedding;
    }

    private async Task<float[]?> GenerateEmbeddingWithOpenAIAsync(string text, AIConfiguration config)
    {
        var apiKey = config.OpenAIApiKey ?? config.ProviderApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("API key di OpenAI non configurata");
        }

        var openAIClient = new OpenAI.OpenAIClient(apiKey);
        var modelName = config.OpenAIEmbeddingModel ?? "text-embedding-ada-002";
        var embeddingClient = openAIClient.GetEmbeddingClient(modelName);
        var response = await embeddingClient.GenerateEmbeddingAsync(text);
        
        var embedding = response.Value.ToFloats().ToArray();
        
        // Log embedding info for debugging
        await _logService.LogDebugAsync("Embedding", $"[OpenAI] Generated embedding: {embedding.Length} dimensions", $"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F6")))}]");
        
        return embedding;
    }

    private async Task<float[]?> GenerateEmbeddingWithGeminiAsync(string text, AIConfiguration config)
    {
        var apiKey = config.GeminiApiKey ?? config.ProviderApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await _logService.LogErrorAsync("Embedding", "API key di Gemini non configurata");
            throw new InvalidOperationException("API key di Gemini non configurata");
        }

        return await ExecuteWithRetryAsync(async () =>
        {
            try
            {
                var gemini = new GoogleAI(apiKey);
                // Ensure model name has proper format - add "models/" prefix if not present
                var modelName = config.GeminiEmbeddingModel ?? "text-embedding-004";
                if (!modelName.StartsWith("models/"))
                {
                    modelName = $"models/{modelName}";
                }
                var model = gemini.GenerativeModel(model: modelName);
                
                await _logService.LogDebugAsync("Embedding", $"[Gemini] Attempting to generate embedding with model: {modelName}");
                var response = await model.EmbedContent(
                    content: text,
                    taskType: Mscc.GenerativeAI.TaskType.RetrievalDocument);
                
                if (response?.Embedding?.Values != null)
                {
                    var embedding = response.Embedding.Values.Select(v => (float)v).ToArray();
                    
                    // Log embedding info for debugging
                    await _logService.LogDebugAsync("Embedding", $"[Gemini] Generated embedding: {embedding.Length} dimensions", $"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F6")))}]");
                    
                    return embedding;
                }
                else
                {
                    await _logService.LogErrorAsync("Embedding", "La risposta di Gemini era nulla o non conteneva valori di embedding");
                    throw new InvalidOperationException("Gemini non ha restituito valori di embedding");
                }
            }
            catch (System.Net.Http.HttpRequestException ex) when (
                (ex.StatusCode.HasValue && ex.StatusCode.Value == System.Net.HttpStatusCode.NotFound) || 
                ex.Message.Contains("NotFound") || 
                ex.Message.Contains("404") ||
                ex.Message.Contains("NOT_FOUND"))
            {
                var modelName = config.GeminiEmbeddingModel ?? "text-embedding-004";
                await _logService.LogErrorAsync("Embedding", $"Modello Gemini embedding non trovato: {modelName}", ex.Message, stackTrace: ex.StackTrace);
                throw new InvalidOperationException($"Il modello Gemini embedding '{modelName}' non è stato trovato o non è supportato. Verifica che il modello sia disponibile per la tua API key e utilizza modelli supportati come 'text-embedding-004'. Errore: {ex.Message}", ex);
            }
            catch (System.Net.Http.HttpRequestException ex) when (
                (ex.StatusCode.HasValue && ex.StatusCode.Value == System.Net.HttpStatusCode.Forbidden) || 
                ex.Message.Contains("Forbidden") || 
                ex.Message.Contains("403"))
            {
                await _logService.LogErrorAsync("Embedding", "Problema con l'API key di Gemini (potrebbe essere non valida o segnalata)", ex.Message, stackTrace: ex.StackTrace);
                throw new InvalidOperationException("L'API key di Gemini non è valida o è stata segnalata come compromessa. Utilizza una chiave API diversa.", ex);
            }
            catch (System.Net.Http.HttpRequestException ex) when (
                (ex.StatusCode.HasValue && ex.StatusCode.Value == System.Net.HttpStatusCode.BadRequest) || 
                ex.Message.Contains("BadRequest") || 
                ex.Message.Contains("400") ||
                ex.Message.Contains("INVALID_ARGUMENT"))
            {
                var modelName = config.GeminiEmbeddingModel ?? "text-embedding-004";
                await _logService.LogErrorAsync("Embedding", $"Errore nel formato del modello Gemini embedding. Modello richiesto: {modelName}", ex.Message, stackTrace: ex.StackTrace);
                throw new InvalidOperationException($"Il modello Gemini embedding '{modelName}' non è valido o non è disponibile. Verifica che il nome del modello sia corretto (es: 'text-embedding-004'). Errore: {ex.Message}", ex);
            }
            catch (System.Net.Http.HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // Check if this is a quota exceeded error (not just temporary rate limit)
                if (IsQuotaExceededError(ex.Message))
                {
                    var modelName = config.GeminiEmbeddingModel ?? "text-embedding-004";
                    await _logService.LogErrorAsync("Embedding", 
                        $"❌ Quota Gemini esaurita per il modello '{modelName}'", 
                        ex.Message, 
                        stackTrace: ex.StackTrace);
                    
                    // Throw a more informative error that won't be retried
                    throw new InvalidOperationException(
                        $"🚫 Quota API Gemini esaurita per il modello embedding '{modelName}'. " +
                        $"L'account ha superato i limiti gratuiti o a pagamento. " +
                        $"Soluzioni:\n" +
                        $"• Verifica il tuo piano e i dettagli di fatturazione su https://ai.google.dev/pricing\n" +
                        $"• Monitora l'utilizzo su https://aistudio.google.com/app/apikey\n" +
                        $"• Passa a un piano a pagamento o attendi il reset della quota\n" +
                        $"• Configura un provider alternativo (OpenAI o Azure OpenAI) come fallback\n" +
                        $"• Modello utilizzato: {modelName} (consigliato: text-embedding-004)\n" +
                        $"Errore originale: {ex.Message}", ex);
                }
                
                // Temporary rate limit - let the retry logic handle this
                throw;
            }
            catch (Exception ex)
            {
                await _logService.LogErrorAsync("Embedding", "Errore embedding Gemini", ex.Message, stackTrace: ex.StackTrace);
                throw; // Re-throw to allow fallback logic to work
            }
        }, "Gemini Embedding");
    }

    public async Task<string> GenerateChatCompletionAsync(string systemPrompt, string userPrompt)
    {
        return await GenerateChatCompletionWithProviderAsync(systemPrompt, userPrompt, providerOverride: null);
    }

    private async Task<string> GenerateChatCompletionWithProviderAsync(string systemPrompt, string userPrompt, AIProviderType? providerOverride)
    {
        var config = await GetActiveConfigurationAsync();
        if (config == null)
        {
            throw new InvalidOperationException("Nessuna configurazione AI attiva trovata nel database o in appsettings.json. Configura un provider AI tramite l'interfaccia utente.");
        }

        // Determine which provider to use for chat (use providerOverride if specified, otherwise ChatProvider, otherwise ProviderType)
        var provider = providerOverride ?? config.ChatProvider ?? config.ProviderType;
        
        // Log the configuration being used
        await _logService.LogInfoAsync("AI", $"Attempting chat with provider: {provider}");
        
        // Check if any API key is configured (with fallback to ProviderApiKey)
        var geminiKey = GetEffectiveApiKey(config.GeminiApiKey, AIProviderType.Gemini, config);
        var openAIKey = GetEffectiveApiKey(config.OpenAIApiKey, AIProviderType.OpenAI, config);
        var azureKey = GetEffectiveApiKey(config.AzureOpenAIKey, AIProviderType.AzureOpenAI, config);
        var azureEndpoint = GetEffectiveEndpoint(config.AzureOpenAIEndpoint, AIProviderType.AzureOpenAI, config);
        var ollamaEndpoint = config.OllamaEndpoint;
        var groqKey = config.GroqApiKey;
        
        bool hasGeminiKey = !string.IsNullOrWhiteSpace(geminiKey);
        bool hasOpenAIKey = !string.IsNullOrWhiteSpace(openAIKey);
        bool hasAzureKey = !string.IsNullOrWhiteSpace(azureKey) && !string.IsNullOrWhiteSpace(azureEndpoint);
        bool hasOllamaEndpoint = !string.IsNullOrWhiteSpace(ollamaEndpoint);
        bool hasGroqKey = !string.IsNullOrWhiteSpace(groqKey);
        
        if (!hasGeminiKey && !hasOpenAIKey && !hasAzureKey && !hasOllamaEndpoint && !hasGroqKey)
        {
            throw new InvalidOperationException("Nessun provider AI ha una chiave API o endpoint valido configurato. Configura almeno un provider (Gemini, OpenAI, Azure OpenAI, Ollama, o Groq) tramite l'interfaccia utente.");
        }

        // Try primary provider
        try
        {
            string result = provider switch
            {
                AIProviderType.Gemini => await GenerateChatWithGeminiAsync(systemPrompt, userPrompt, config),
                AIProviderType.OpenAI => await GenerateChatWithOpenAIAsync(systemPrompt, userPrompt, config),
                AIProviderType.AzureOpenAI => await GenerateChatWithAzureOpenAIAsync(systemPrompt, userPrompt, config),
                AIProviderType.Ollama => await GenerateChatWithOllamaAsync(systemPrompt, userPrompt, config),
                AIProviderType.Groq => await GenerateChatWithGroqAsync(systemPrompt, userPrompt, config),
                _ => throw new InvalidOperationException($"Provider non supportato: {provider}")
            };
            
            await _logService.LogInfoAsync("AI", $"Chat generation successful with {provider}");
            return result;
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync("AI", $"Provider di chat primario ({provider}) fallito", ex.Message, stackTrace: ex.StackTrace);
            
            // If primary fails and fallback is enabled, try the alternative provider
            if (config.EnableFallback)
            {
                var errors = new List<string> { $"{provider}: {ex.Message}" };
                
                // Try Gemini as fallback
                if (provider != AIProviderType.Gemini && hasGeminiKey)
                {
                    try
                    {
                        await _logService.LogInfoAsync("AI", "Tentativo Gemini come fallback");
                        var result = await GenerateChatWithGeminiAsync(systemPrompt, userPrompt, config);
                        await _logService.LogInfoAsync("AI", "Chat generation successful with Gemini (fallback)");
                        return result;
                    }
                    catch (Exception ex2)
                    {
                        await _logService.LogWarningAsync("AI", "Fallback Gemini fallito", ex2.Message);
                        errors.Add($"Gemini: {ex2.Message}");
                    }
                }
                
                // Try OpenAI as fallback
                if (provider != AIProviderType.OpenAI && hasOpenAIKey)
                {
                    try
                    {
                        await _logService.LogInfoAsync("AI", "Tentativo OpenAI come fallback");
                        var result = await GenerateChatWithOpenAIAsync(systemPrompt, userPrompt, config);
                        await _logService.LogInfoAsync("AI", "Chat generation successful with OpenAI (fallback)");
                        return result;
                    }
                    catch (Exception ex3)
                    {
                        await _logService.LogWarningAsync("AI", "Fallback OpenAI fallito", ex3.Message);
                        errors.Add($"OpenAI: {ex3.Message}");
                    }
                }
                
                // Try Azure OpenAI as fallback
                if (provider != AIProviderType.AzureOpenAI && hasAzureKey)
                {
                    try
                    {
                        await _logService.LogInfoAsync("AI", "Tentativo AzureOpenAI come fallback");
                        var result = await GenerateChatWithAzureOpenAIAsync(systemPrompt, userPrompt, config);
                        await _logService.LogInfoAsync("AI", "Chat generation successful with Azure OpenAI (fallback)");
                        return result;
                    }
                    catch (Exception ex4)
                    {
                        await _logService.LogErrorAsync("AI", "Fallback AzureOpenAI fallito", ex4.Message, stackTrace: ex4.StackTrace);
                        errors.Add($"AzureOpenAI: {ex4.Message}");
                    }
                }
                
                // Try Ollama as fallback
                if (provider != AIProviderType.Ollama && hasOllamaEndpoint)
                {
                    try
                    {
                        await _logService.LogInfoAsync("AI", "Tentativo Ollama come fallback");
                        var result = await GenerateChatWithOllamaAsync(systemPrompt, userPrompt, config);
                        await _logService.LogInfoAsync("AI", "Chat generation successful with Ollama (fallback)");
                        return result;
                    }
                    catch (Exception ex5)
                    {
                        await _logService.LogWarningAsync("AI", "Fallback Ollama fallito", ex5.Message);
                        errors.Add($"Ollama: {ex5.Message}");
                    }
                }
                
                // Try Groq as fallback
                if (provider != AIProviderType.Groq && hasGroqKey)
                {
                    try
                    {
                        await _logService.LogInfoAsync("AI", "Tentativo Groq come fallback");
                        var result = await GenerateChatWithGroqAsync(systemPrompt, userPrompt, config);
                        await _logService.LogInfoAsync("AI", "Chat generation successful with Groq (fallback)");
                        return result;
                    }
                    catch (Exception ex6)
                    {
                        await _logService.LogWarningAsync("AI", "Fallback Groq fallito", ex6.Message);
                        errors.Add($"Groq: {ex6.Message}");
                    }
                }
                
                // All attempts failed
                var errorMessage = $"Tutti i provider AI sono falliti. Errori: {string.Join("; ", errors)}";
                
                // Check if any error indicates a quota issue (formatted messages use 🚫, raw errors have quota keywords)
                var hasQuotaIssue = errors.Any(e => e.Contains("🚫") || IsQuotaExceededError(e));
                if (hasQuotaIssue)
                {
                    errorMessage += "\n\n💡 Suggerimento: Uno o più provider hanno esaurito la quota. " +
                                   "Configura un provider alternativo con quota disponibile o attendi il reset della quota.";
                }
                
                await _logService.LogErrorAsync("AI", "Tutti i fallback sono falliti", errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            else
            {
                // Fallback disabled, just throw the original error
                throw new InvalidOperationException($"Provider {provider} fallito: {ex.Message}. Fallback disabilitato.");
            }
        }
    }

    private async Task<string> GenerateChatWithGeminiAsync(string systemPrompt, string userPrompt, AIConfiguration config)
    {
        var apiKey = config.GeminiApiKey ?? config.ProviderApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await _logService.LogErrorAsync("AI", "API key di Gemini non configurata");
            throw new InvalidOperationException("API key di Gemini non configurata");
        }

        return await ExecuteWithRetryAsync(async () =>
        {
            try
            {
                var gemini = new GoogleAI(apiKey);
                // Ensure model name has proper format - add "models/" prefix if not present
                var modelName = config.GeminiChatModel ?? "gemini-2.0-flash-exp";
                if (!modelName.StartsWith("models/"))
                {
                    modelName = $"models/{modelName}";
                }
                var model = gemini.GenerativeModel(model: modelName);
                
                var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";
                await _logService.LogDebugAsync("AI", $"[Gemini] Attempting to generate chat with model: {modelName}");
                var response = await model.GenerateContent(fullPrompt);
                
                if (response?.Text != null)
                {
                    await _logService.LogDebugAsync("AI", "Risposta di chat Gemini generata con successo");
                    return response.Text;
                }
                else
                {
                    await _logService.LogErrorAsync("AI", "La risposta di chat Gemini era nulla");
                    throw new InvalidOperationException("Nessuna risposta da Gemini");
                }
            }
            catch (System.Net.Http.HttpRequestException ex) when (
                (ex.StatusCode.HasValue && ex.StatusCode.Value == System.Net.HttpStatusCode.NotFound) || 
                ex.Message.Contains("NotFound") || 
                ex.Message.Contains("404") ||
                ex.Message.Contains("NOT_FOUND"))
            {
                var modelName = config.GeminiChatModel ?? "gemini-2.0-flash-exp";
                await _logService.LogErrorAsync("AI", $"Modello Gemini non trovato: {modelName}", ex.Message, stackTrace: ex.StackTrace);
                throw new InvalidOperationException($"Il modello Gemini '{modelName}' non è stato trovato o non è supportato. I modelli più vecchi come 'gemini-1.5-flash' potrebbero non essere più disponibili. Prova a utilizzare modelli più recenti come 'gemini-2.0-flash-exp', 'gemini-2.5-flash', o 'gemini-3-flash'. Puoi anche verificare i modelli disponibili con l'API ListModels. Errore: {ex.Message}", ex);
            }
            catch (System.Net.Http.HttpRequestException ex) when (
                (ex.StatusCode.HasValue && ex.StatusCode.Value == System.Net.HttpStatusCode.Forbidden) || 
                ex.Message.Contains("Forbidden") || 
                ex.Message.Contains("403"))
            {
                await _logService.LogErrorAsync("AI", "Problema con l'API key di Gemini (potrebbe essere non valida o segnalata)", ex.Message, stackTrace: ex.StackTrace);
                throw new InvalidOperationException("L'API key di Gemini non è valida o è stata segnalata come compromessa. Utilizza una chiave API diversa.", ex);
            }
            catch (System.Net.Http.HttpRequestException ex) when (
                (ex.StatusCode.HasValue && ex.StatusCode.Value == System.Net.HttpStatusCode.BadRequest) || 
                ex.Message.Contains("BadRequest") || 
                ex.Message.Contains("400") ||
                ex.Message.Contains("INVALID_ARGUMENT"))
            {
                var modelName = config.GeminiChatModel ?? "gemini-2.0-flash-exp";
                await _logService.LogErrorAsync("AI", $"Errore nel formato del modello Gemini. Modello richiesto: {modelName}", ex.Message, stackTrace: ex.StackTrace);
                throw new InvalidOperationException($"Il modello Gemini '{modelName}' non è valido o non è disponibile. Verifica che il nome del modello sia corretto (es: 'gemini-2.0-flash-exp', 'gemini-2.5-flash', 'gemini-3-flash'). Errore: {ex.Message}", ex);
            }
            catch (System.Net.Http.HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // Check if this is a quota exceeded error (not just temporary rate limit)
                if (IsQuotaExceededError(ex.Message))
                {
                    var modelName = config.GeminiChatModel ?? "gemini-2.0-flash-exp";
                    await _logService.LogErrorAsync("AI", 
                        $"❌ Quota Gemini esaurita per il modello '{modelName}'", 
                        ex.Message, 
                        stackTrace: ex.StackTrace);
                    
                    // Throw a more informative error that won't be retried
                    throw new InvalidOperationException(
                        $"🚫 Quota API Gemini esaurita per il modello chat '{modelName}'. " +
                        $"L'account ha superato i limiti gratuiti o a pagamento. " +
                        $"Soluzioni:\n" +
                        $"• Verifica il tuo piano e i dettagli di fatturazione su https://ai.google.dev/pricing\n" +
                        $"• Monitora l'utilizzo su https://aistudio.google.com/app/apikey\n" +
                        $"• Passa a un piano a pagamento o attendi il reset della quota\n" +
                        $"• Configura un provider alternativo (OpenAI o Azure OpenAI) come fallback\n" +
                        $"• Modello utilizzato: {modelName} (consigliati: gemini-2.0-flash-exp, gemini-1.5-flash)\n" +
                        $"Errore originale: {ex.Message}", ex);
                }
                
                // Temporary rate limit - let the retry logic handle this
                throw;
            }
            catch (Exception ex)
            {
                await _logService.LogErrorAsync("AI", "Errore chat Gemini", ex.Message, stackTrace: ex.StackTrace);
                throw;
            }
        }, "Gemini Chat");
    }

    private async Task<string> GenerateChatWithOpenAIAsync(string systemPrompt, string userPrompt, AIConfiguration config)
    {
        var apiKey = config.OpenAIApiKey ?? config.ProviderApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("API key di OpenAI non configurata");

        var openAIClient = new OpenAI.OpenAIClient(apiKey);
        var modelName = config.OpenAIChatModel ?? "gpt-4";
        var chatClient = openAIClient.GetChatClient(modelName);
        
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            OpenAI.Chat.ChatMessage.CreateSystemMessage(systemPrompt),
            OpenAI.Chat.ChatMessage.CreateUserMessage(userPrompt)
        };
        
        var completion = await chatClient.CompleteChatAsync(messages);
        return completion.Value.Content[0].Text;
    }

    private async Task<string> GenerateChatWithAzureOpenAIAsync(string systemPrompt, string userPrompt, AIConfiguration config)
    {
        var endpoint = config.AzureOpenAIEndpoint ?? config.ProviderEndpoint;
        var apiKey = config.AzureOpenAIKey ?? config.ProviderApiKey;
        var deploymentName = config.ChatDeploymentName ?? config.AzureOpenAIChatModel ?? "gpt-4";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Endpoint e API key di Azure OpenAI non configurati");
        }

        var azureClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));

        var chatClient = azureClient.GetChatClient(deploymentName);

        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            OpenAI.Chat.ChatMessage.CreateSystemMessage(systemPrompt),
            OpenAI.Chat.ChatMessage.CreateUserMessage(userPrompt)
        };

        var completion = await chatClient.CompleteChatAsync(messages);
        return completion.Value.Content[0].Text;
    }

    private async Task<string> GenerateChatWithOllamaAsync(string systemPrompt, string userPrompt, AIConfiguration config)
    {
        var endpoint = config.OllamaEndpoint ?? "http://localhost:11434";
        var model = config.OllamaChatModel ?? "llama3";

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Endpoint di Ollama non configurato");
        }

        var ollamaClient = new OllamaSharp.OllamaApiClient(new Uri(endpoint));
        ollamaClient.SelectedModel = model;
        
        var chat = new OllamaSharp.Chat(ollamaClient);
        var fullPrompt = $"System: {systemPrompt}\n\nUser: {userPrompt}";
        
        var responseBuilder = new System.Text.StringBuilder();
        await foreach (var token in chat.SendAsync(fullPrompt))
        {
            responseBuilder.Append(token);
        }
        
        var response = responseBuilder.ToString();
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException("Ollama returned empty response");
        }
        
        return response;
    }

    private async Task<string> GenerateChatWithGroqAsync(string systemPrompt, string userPrompt, AIConfiguration config)
    {
        var apiKey = config.GroqApiKey;
        var endpoint = config.GroqEndpoint ?? "https://api.groq.com/openai/v1";
        var model = config.GroqChatModel ?? "llama-3.1-8b-instant";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("API key di Groq non configurata");
        }

        var options = new OpenAI.OpenAIClientOptions();
        options.Endpoint = new Uri(endpoint);
        
        var groqClient = new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
        var chatClient = groqClient.GetChatClient(model);
        
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            OpenAI.Chat.ChatMessage.CreateSystemMessage(systemPrompt),
            OpenAI.Chat.ChatMessage.CreateUserMessage(userPrompt)
        };
        
        var completion = await chatClient.CompleteChatAsync(messages);
        return completion.Value.Content[0].Text;
    }

    public async Task<(string Category, string Reasoning, string Provider)> SuggestCategoryAsync(string fileName, string extractedText)
    {
        try
        {
            return await ExecuteWithTimeoutAsync(async (cancellationToken) =>
            {
                // Get provider asynchronously to avoid deadlock
                var provider = await GetCurrentChatProviderAsync();
                
                // Get existing categories from database
                var existingCategories = await _context.Documents
                    .Where(d => !string.IsNullOrEmpty(d.ActualCategory))
                    .Select(d => d.ActualCategory)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var categoriesHint = existingCategories.Any()
                    ? $"Existing categories in the system: {string.Join(", ", existingCategories)}. You can suggest one of these or propose a new category if none fit."
                    : "This is a new system. You MUST suggest an appropriate category based on the document content.";

                var systemPrompt = @"You are a document classification expert. Your task is to ALWAYS suggest a specific, meaningful category for documents.
NEVER return 'Uncategorized' or generic categories. Analyze the content and propose a descriptive category name.
Examples of good categories: 'Financial Reports', 'Legal Contracts', 'Technical Documentation', 'Marketing Materials', 'Meeting Minutes', etc.
Always respond in Italian.";
            
                var userPrompt = $@"Analizza questo documento e suggerisci la MIGLIORE categoria possibile. Spiega anche la tua motivazione.

Nome file: {fileName}
Anteprima contenuto: {TruncateText(extractedText, 500)}

{categoriesHint}

IMPORTANTE: DEVI suggerire una categoria specifica. Se nessuna categoria esistente si adatta perfettamente, proponi un nuovo nome di categoria descrittivo basato sul contenuto del documento.
NON usare termini generici come 'Uncategorized', 'General' o 'Other'.

Rispondi in formato JSON:
{{
    ""category"": ""nome categoria specifico"",
    ""reasoning"": ""spiegazione dettagliata del motivo per cui questa categoria si adatta, menzionando parole chiave specifiche, tipo di contenuto o pattern identificati""
}}";

                // TODO: Pass cancellationToken to GenerateChatCompletionAsync to properly cancel underlying HTTP requests
                // Currently the timeout prevents indefinite waits but doesn't cancel in-flight AI API calls
                var response = await GenerateChatCompletionAsync(systemPrompt, userPrompt);
            
                // Clean up response - sometimes AI adds markdown code blocks
                response = response.Trim();
                if (response.StartsWith("```json"))
                {
                    response = response.Substring(7);
                    if (response.EndsWith("```"))
                        response = response.Substring(0, response.Length - 3);
                }
                else if (response.StartsWith("```"))
                {
                    response = response.Substring(3);
                    if (response.EndsWith("```"))
                        response = response.Substring(0, response.Length - 3);
                }
                response = response.Trim();
            
                // Parse JSON response
                CategorySuggestion? result = null;
                try
                {
                    result = System.Text.Json.JsonSerializer.Deserialize<CategorySuggestion>(response, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (System.Text.Json.JsonException)
                {
                    // If JSON parsing fails, try to extract category from response text
                    await _logService.LogWarningAsync("Category", "Failed to parse JSON response", response);
                }
            
                // Validate that we got a meaningful category
                var category = result?.Category?.Trim() ?? string.Empty;
                var reasoning = result?.Reasoning?.Trim() ?? "Nessuna motivazione fornita";
            
                // If AI still returned generic/empty category, infer from filename or content
                if (string.IsNullOrEmpty(category) || 
                    category.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase) ||
                    category.Equals("General", StringComparison.OrdinalIgnoreCase) ||
                    category.Equals("Other", StringComparison.OrdinalIgnoreCase))
                {
                    // Try to infer from file extension or name
                    category = InferCategoryFromFileNameOrContent(fileName, extractedText);
                    reasoning = $"Categoria inferita dal nome file o contenuto. {reasoning}";
                }
            
                return (category, reasoning, provider);
            }, "CategorySuggestion");
        }
        catch (TimeoutException tex)
        {
            // Timeout occurred - return inferred category with timeout message
            await _logService.LogWarningAsync("Category", "Category suggestion timed out", tex.Message);
            var inferredCategory = InferCategoryFromFileNameOrContent(fileName, extractedText);
            var provider = await GetCurrentChatProviderAsync();
            return (inferredCategory, 
                $"⏱️ Timeout: L'analisi AI ha richiesto troppo tempo. Categoria inferita dal nome del file. {tex.Message}", 
                provider);
        }
        catch (Exception ex)
        {
            // Even on error, try to return something meaningful instead of "Uncategorized"
            var inferredCategory = InferCategoryFromFileNameOrContent(fileName, extractedText);
            var provider = await GetCurrentChatProviderAsync();
            return (inferredCategory, $"Errore nell'analisi AI: {ex.Message}. Categoria inferita dal nome del file.", provider);
        }
    }
    
    private string InferCategoryFromFileNameOrContent(string fileName, string extractedText)
    {
        // Try to infer category from file name
        var lowerFileName = fileName.ToLowerInvariant();
        
        // Check for common document type patterns in filename
        if (lowerFileName.Contains("contract") || lowerFileName.Contains("contratto"))
            return "Legal Contracts";
        if (lowerFileName.Contains("invoice") || lowerFileName.Contains("fattura"))
            return "Financial Documents";
        if (lowerFileName.Contains("report") || lowerFileName.Contains("rapporto"))
            return "Reports";
        if (lowerFileName.Contains("meeting") || lowerFileName.Contains("minutes") || lowerFileName.Contains("verbale"))
            return "Meeting Minutes";
        if (lowerFileName.Contains("proposal") || lowerFileName.Contains("proposta"))
            return "Proposals";
        if (lowerFileName.Contains("manual") || lowerFileName.Contains("manuale") || lowerFileName.Contains("guide"))
            return "Documentation";
        if (lowerFileName.Contains("letter") || lowerFileName.Contains("lettera"))
            return "Correspondence";
        
        // Check file extension to infer document type
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".pdf")
            return "PDF Documents";
        if (extension == ".docx" || extension == ".doc")
            return "Word Documents";
        if (extension == ".xlsx" || extension == ".xls")
            return "Spreadsheets";
        if (extension == ".txt")
            return "Text Documents";
        
        // Try to infer from content if available
        if (!string.IsNullOrEmpty(extractedText))
        {
            var lowerContent = extractedText.ToLowerInvariant();
            if (lowerContent.Contains("contract") || lowerContent.Contains("agreement"))
                return "Legal Documents";
            if (lowerContent.Contains("invoice") || lowerContent.Contains("payment"))
                return "Financial Documents";
            if (lowerContent.Contains("meeting") || lowerContent.Contains("agenda"))
                return "Meeting Documents";
        }
        
        // Last resort - use file extension based category
        if (string.IsNullOrEmpty(extension) || extension == ".")
            return "Unknown Files";
        
        return $"{extension.TrimStart('.')} Files";
    }

    public async Task<List<string>> ExtractTagsAsync(string extractedText)
    {
        try
        {
            return await ExecuteWithTimeoutAsync(async (cancellationToken) =>
            {
                // Get configuration to determine correct provider for tag extraction
                var config = await GetActiveConfigurationAsync();
                if (config == null)
                {
                    throw new InvalidOperationException("Nessuna configurazione AI attiva trovata nel database. Configura un provider AI tramite l'interfaccia utente.");
                }

                // Use TagExtractionProvider if specified, otherwise fall back to ChatProvider or ProviderType
                var provider = config.TagExtractionProvider ?? config.ChatProvider ?? config.ProviderType;
                
                await _logService.LogInfoAsync("Tag", $"Extracting tags with provider: {provider}");
                
                var systemPrompt = "You are a tag extraction expert. Extract 5-10 relevant keywords or tags from documents.";
            
                var userPrompt = $@"Extract 5-10 relevant tags or keywords from this document.
Tags should be short, specific, and representative of the content.

Content: {TruncateText(extractedText, 1000)}

Respond in JSON format:
{{
    ""tags"": [""tag1"", ""tag2"", ""tag3"", ...]
}}";

                // TODO: Pass cancellationToken to GenerateChatCompletionAsync to properly cancel underlying HTTP requests
                // Currently the timeout prevents indefinite waits but doesn't cancel in-flight AI API calls
                var response = await GenerateChatCompletionWithProviderAsync(systemPrompt, userPrompt, provider);
            
                // Clean up response - sometimes AI adds markdown code blocks
                response = response.Trim();
                if (response.StartsWith("```json"))
                {
                    response = response.Substring(7);
                    if (response.EndsWith("```"))
                        response = response.Substring(0, response.Length - 3);
                }
                else if (response.StartsWith("```"))
                {
                    response = response.Substring(3);
                    if (response.EndsWith("```"))
                        response = response.Substring(0, response.Length - 3);
                }
                response = response.Trim();
            
                // Parse JSON response
                TagsResponse? result = null;
                try
                {
                    result = System.Text.Json.JsonSerializer.Deserialize<TagsResponse>(response, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (System.Text.Json.JsonException jsonEx)
                {
                    // If JSON parsing fails, log it
                    await _logService.LogWarningAsync("Tag", "Failed to parse tags JSON response", $"Response: {response}\nJSON error: {jsonEx.Message}");
                }
            
                return result?.Tags ?? new List<string>();
            }, "TagExtraction");
        }
        catch (TimeoutException tex)
        {
            await _logService.LogWarningAsync("Tag", "Tag extraction timed out", tex.Message);
            return new List<string>(); // Return empty list on timeout
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync("Tag", "Error extracting tags", ex.Message, stackTrace: ex.StackTrace);
            return new List<string>();
        }
    }

    public async Task<Dictionary<string, string>> ExtractMetadataAsync(string extractedText, string fileName = "")
    {
        try
        {
            // Get configuration to determine correct provider for metadata extraction
            var config = await GetActiveConfigurationAsync();
            if (config == null)
            {
                throw new InvalidOperationException("Nessuna configurazione AI attiva trovata nel database. Configura un provider AI tramite l'interfaccia utente.");
            }

            // Use MetadataExtractionProvider if specified, otherwise fall back to ChatProvider or ProviderType
            var provider = config.MetadataExtractionProvider ?? config.ChatProvider ?? config.ProviderType;
            
            await _logService.LogInfoAsync("Metadata", $"Extracting metadata with provider: {provider}", fileName);
            
            var systemPrompt = "You are a metadata extraction expert. Extract structured metadata from documents.";
            
            var userPrompt = $@"Extract structured metadata from this document. Analyze the content and identify key information.

File name: {fileName}

Content: {TruncateText(extractedText, 3000)}

Extract as many relevant metadata fields as possible, for example:
- For INVOICES: invoice_number, invoice_date, invoice_year, total_amount, currency, vendor_name, customer_name, tax_id, payment_terms
- For CONTRACTS: contract_number, contract_date, contract_year, parties, expiration_date, renewal_terms, contract_value
- For GENERAL DOCUMENTS: document_type, author, creation_date, title, subject, company_name, reference_number, language
- Other relevant metadata specific to the document type

Respond in JSON format with key-value pairs.
Use English field names in snake_case (e.g., invoice_number, creation_date).
If a field is not present, DO NOT include it in the result.

Example: {{""document_type"": ""invoice"", ""invoice_number"": ""INV-2024-001"", ""invoice_date"": ""2024-01-15"", ""total_amount"": ""1000.00"", ""currency"": ""EUR""}}

Respond ONLY with valid JSON, no other comments.";

            var response = await GenerateChatCompletionWithProviderAsync(systemPrompt, userPrompt, provider);
            
            // Clean up response
            response = response.Trim();
            if (response.StartsWith("```json"))
            {
                response = response.Substring(7);
                if (response.EndsWith("```"))
                    response = response.Substring(0, response.Length - 3);
            }
            else if (response.StartsWith("```"))
            {
                response = response.Substring(3);
                if (response.EndsWith("```"))
                    response = response.Substring(0, response.Length - 3);
            }
            response = response.Trim();
            
            // Parse JSON response - handle mixed value types (strings, numbers, booleans, etc.)
            try
            {
                using var jsonDoc = JsonDocument.Parse(response);
                var metadata = new Dictionary<string, string>();
                
                foreach (var property in jsonDoc.RootElement.EnumerateObject())
                {
                    // Convert all JSON value types to strings
                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => property.Value.GetRawText(), // Get the raw number as string
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => string.Empty,
                        JsonValueKind.Object => property.Value.GetRawText(), // Nested objects as JSON string
                        JsonValueKind.Array => property.Value.GetRawText(), // Arrays as JSON string
                        _ => property.Value.GetRawText()
                    };
                    
                    if (!string.IsNullOrEmpty(value))
                    {
                        metadata[property.Name] = value;
                    }
                }
                
                await _logService.LogDebugAsync("Metadata", $"Extracted {metadata.Count} metadata fields", fileName);
                return metadata;
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                await _logService.LogWarningAsync("Metadata", "Failed to parse metadata JSON response", $"Response: {response}\nJSON error: {jsonEx.Message}", fileName: fileName);
                return new Dictionary<string, string>();
            }
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync("Metadata", "Error extracting metadata", ex.Message, fileName: fileName, stackTrace: ex.StackTrace);
            return new Dictionary<string, string>();
        }
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Extracts retry delay from Gemini API error response
    /// </summary>
    private TimeSpan? ExtractRetryDelayFromError(string errorMessage)
    {
        try
        {
            // Try to parse the JSON error response
            var startIndex = errorMessage.IndexOf("{");
            if (startIndex >= 0)
            {
                var jsonPart = errorMessage.Substring(startIndex);
                using (JsonDocument doc = JsonDocument.Parse(jsonPart))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("error", out var error) &&
                        error.TryGetProperty("details", out var details) &&
                        details.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var detail in details.EnumerateArray())
                        {
                            if (detail.TryGetProperty("@type", out var type) &&
                                type.GetString()?.Contains("RetryInfo") == true &&
                                detail.TryGetProperty("retryDelay", out var retryDelay))
                            {
                                var delayStr = retryDelay.GetString();
                                if (!string.IsNullOrEmpty(delayStr) && delayStr.EndsWith("s"))
                                {
                                    // Parse delay like "23.509312193s" to seconds
                                    var secondsStr = delayStr.TrimEnd('s');
                                    if (double.TryParse(secondsStr, System.Globalization.NumberStyles.Float, 
                                        System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                                    {
                                        return TimeSpan.FromSeconds(Math.Ceiling(seconds));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // If parsing fails, return null to use default retry delay
            // Don't log here as this is called in error handling path
        }

        return null;
    }

    /// <summary>
    /// Checks if the error is a quota exceeded error (not just temporary rate limit)
    /// </summary>
    private bool IsQuotaExceededError(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return false;
            
        // Check for quota-related keywords in the error message
        var lowerMessage = errorMessage.ToLowerInvariant();
        return lowerMessage.Contains("quota exceeded") || 
               lowerMessage.Contains("current quota") ||
               lowerMessage.Contains("billing details") ||
               lowerMessage.Contains("limit: 0") ||
               lowerMessage.Contains("free tier") ||
               lowerMessage.Contains("free_tier");
    }

    /// <summary>
    /// Executes an async function with retry logic for rate limit (429) errors
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxRetries = 3)
    {
        int retryCount = 0;
        TimeSpan baseDelay = TimeSpan.FromSeconds(5);
        Exception? lastException = null;

        while (retryCount <= maxRetries)
        {
            try
            {
                return await operation();
            }
            catch (System.Net.Http.HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests && 
                retryCount < maxRetries)
            {
                retryCount++;
                lastException = ex;
                
                // Try to extract retry delay from API response
                TimeSpan delay = ExtractRetryDelayFromError(ex.Message) 
                    ?? TimeSpan.FromSeconds(baseDelay.TotalSeconds * Math.Pow(2, retryCount - 1));

                await _logService.LogWarningAsync(
                    operationName, 
                    $"Rate limit exceeded (429). Retry {retryCount}/{maxRetries}", 
                    $"Waiting {delay.TotalSeconds:F1} seconds before retry");

                await Task.Delay(delay);
                
                await _logService.LogInfoAsync(operationName, $"Retrying after rate limit... Attempt {retryCount + 1}");
            }
            catch (Exception)
            {
                // For non-retryable errors or when retries exhausted, throw immediately
                throw;
            }
        }

        // If we've exhausted retries, throw the last exception
        throw lastException ?? new InvalidOperationException($"Operation failed after {maxRetries} retries");
    }

    /// <summary>
    /// Executes an AI operation with timeout protection
    /// </summary>
    private async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation, 
        string operationName)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(_aiTimeoutSeconds));
        
        try
        {
            await _logService.LogDebugAsync(operationName, 
                $"Starting AI operation with timeout of {_aiTimeoutSeconds} seconds");
            
            var result = await operation(cts.Token);
            
            await _logService.LogDebugAsync(operationName, 
                "AI operation completed successfully");
            
            return result;
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            await _logService.LogWarningAsync(operationName, 
                $"AI operation timed out after {_aiTimeoutSeconds} seconds",
                "Consider increasing AI:TimeoutSeconds in appsettings or checking AI provider health");
            
            throw new TimeoutException(
                $"L'operazione AI ha superato il tempo limite di {_aiTimeoutSeconds} secondi. " +
                "Il provider AI potrebbe essere sovraccarico o non disponibile. " +
                "Riprova più tardi o contatta l'amministratore.");
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync(operationName, 
                "AI operation failed", 
                ex.Message, 
                stackTrace: ex.StackTrace);
            throw;
        }
    }

    private class CategorySuggestion
    {
        public string Category { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
    }

    private class TagsResponse
    {
        public List<string> Tags { get; set; } = new List<string>();
    }

    public async Task<string> GetCurrentChatProviderAsync()
    {
        var config = await GetActiveConfigurationAsync();
        if (config == null) return "Nessuno";
        
        var provider = config.ChatProvider ?? config.ProviderType;
        return provider.ToString();
    }

    public async Task<string> GetCurrentEmbeddingProviderAsync()
    {
        var config = await GetActiveConfigurationAsync();
        if (config == null) return "Nessuno";
        
        var provider = config.EmbeddingsProvider ?? config.ProviderType;
        return provider.ToString();
    }

    // Synchronous wrappers for backward compatibility
    public string GetCurrentChatProvider()
    {
        return GetCurrentChatProviderAsync().GetAwaiter().GetResult();
    }

    public string GetCurrentEmbeddingProvider()
    {
        return GetCurrentEmbeddingProviderAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Clears the cached configuration to force reload from database on next access.
    /// Useful after updating configuration.
    /// </summary>
    public void ClearConfigurationCache()
    {
        _cachedConfig = null;
        _lastConfigCheck = DateTime.MinValue;
    }
}
