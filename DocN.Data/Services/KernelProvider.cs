using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace DocN.Data.Services;

/// <summary>
/// Fornisce accesso lazy-loaded a un Kernel Semantic Kernel configurato da database
/// </summary>
/// <remarks>
/// <para><strong>Scopo:</strong> Risolvere il problema del "chicken-and-egg" di configurazione Semantic Kernel da database</para>
/// 
/// <para><strong>Problema risolto:</strong></para>
/// <list type="bullet">
/// <item><description>Semantic Kernel richiede configurazione AI (API keys, endpoints)</description></item>
/// <item><description>Configurazione AI è memorizzata nel database (tabella AIConfigurations)</description></item>
/// <item><description>Database non è disponibile durante inizializzazione DI container</description></item>
/// <item><description>Servizi che dipendono da Kernel non possono essere costruiti prima che DB sia pronto</description></item>
/// </list>
/// 
/// <para><strong>Soluzione:</strong> Lazy initialization con thread-safe singleton pattern</para>
/// <list type="number">
/// <item><description>KernelProvider viene registrato in DI come singleton</description></item>
/// <item><description>GetKernelAsync() ritorna sempre lo stesso Kernel (cached)</description></item>
/// <item><description>Prima chiamata a GetKernelAsync() inizializza Kernel da DB</description></item>
/// <item><description>SemaphoreSlim garantisce thread-safety (solo 1 thread inizializza)</description></item>
/// <item><description>Retry automatico su fallimento inizializzazione</description></item>
/// </list>
/// </remarks>
public interface IKernelProvider
{
    /// <summary>
    /// Ottiene istanza Kernel Semantic Kernel configurata da database
    /// </summary>
    /// <returns>Kernel configurato con provider AI (OpenAI/Azure/Gemini) da database</returns>
    /// <remarks>
    /// <para><strong>Lazy initialization:</strong> Kernel viene creato al primo accesso e poi cached</para>
    /// <para><strong>Thread-safe:</strong> Usa SemaphoreSlim per garantire singola inizializzazione</para>
    /// <para><strong>Retry on failure:</strong> Se inizializzazione fallisce, cache viene resettata per retry successivo</para>
    /// </remarks>
    Task<Kernel> GetKernelAsync();
}

/// <summary>
/// Implementazione IKernelProvider che crea Kernel lazy da configurazione database
/// </summary>
/// <remarks>
/// <para><strong>Pattern implementati:</strong></para>
/// <list type="bullet">
/// <item><description>Lazy Initialization - Kernel creato solo quando necessario</description></item>
/// <item><description>Singleton - Stesso Kernel riutilizzato per tutte le richieste</description></item>
/// <item><description>Double-Check Locking - Ottimizzazione per evitare lock inutili</description></item>
/// <item><description>Retry Pattern - Reset cache su fallimento per permettere retry</description></item>
/// </list>
/// 
/// <para><strong>Thread-safety:</strong> SemaphoreSlim(1,1) garantisce che solo 1 thread alla volta possa inizializzare</para>
/// </remarks>
public class KernelProvider : IKernelProvider, IDisposable
{
    private readonly ISemanticKernelFactory _factory;
    private readonly ILogger<KernelProvider> _logger;
    private Task<Kernel>? _kernelTask;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Inizializza una nuova istanza del provider Kernel
    /// </summary>
    /// <param name="factory">Factory per creare Kernel da configurazione database</param>
    /// <param name="logger">Logger per diagnostica inizializzazione</param>
    public KernelProvider(
        ISemanticKernelFactory factory,
        ILogger<KernelProvider> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Se il provider è stato disposed</exception>
    public async Task<Kernel> GetKernelAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(KernelProvider));
        }

        if (_kernelTask != null)
        {
            try
            {
                return await _kernelTask;
            }
            catch
            {
                // If the cached task failed, reset it to allow retry
                await _semaphore.WaitAsync();
                try
                {
                    _kernelTask = null;
                }
                finally
                {
                    _semaphore.Release();
                }
                throw;
            }
        }

        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_kernelTask != null)
            {
                return await _kernelTask;
            }

            _logger.LogInformation("Initializing Semantic Kernel from database configuration...");
            _kernelTask = _factory.CreateKernelAsync();
            var kernel = await _kernelTask;
            _logger.LogInformation("Semantic Kernel initialized successfully");
            
            return kernel;
        }
        catch (Exception ex)
        {
            // Reset on failure to allow retry
            _kernelTask = null;
            _logger.LogError(ex, "Failed to initialize Semantic Kernel");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Rilascia risorse utilizzate dal provider (semaphore)
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _semaphore.Dispose();
        _disposed = true;
    }
}
