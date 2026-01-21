using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DocN.Core.Interfaces;
using Tesseract;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DocN.Data.Services;

/// <summary>
/// Servizio OCR (Optical Character Recognition) che utilizza Tesseract per estrarre testo da immagini
/// Supporta preprocessing immagini e riconoscimento multi-lingua
/// </summary>
/// <remarks>
/// Scopo: Convertire immagini e documenti scansionati in testo ricercabile e indicizzabile
/// 
/// Funzionalità:
/// - Estrazione testo da immagini (PNG, JPG, TIFF, BMP, etc.)
/// - Supporto multi-lingua (ITA, ENG, FRA, DEU, etc.)
/// - Preprocessing automatico immagini per migliorare accuratezza
/// - Gestione robusta errori e fallback graceful
/// 
/// Requisiti:
/// - Tesseract OCR installato sul sistema
/// - Cartella tessdata con file linguaggio (.traineddata)
/// - Path configurato in appsettings.json (Tesseract:DataPath)
/// 
/// Limitazioni:
/// - Qualità OCR dipende da risoluzione e qualità immagine
/// - Migliori risultati con: immagini ad alta risoluzione (300+ DPI), testo chiaro, buona illuminazione
/// - Performance: ~1-3 secondi per immagine (dipende da dimensione e complessità)
/// </remarks>
public class TesseractOCRService : IOCRService
{
    private readonly ILogger<TesseractOCRService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _tessDataPath;
    private bool _isAvailable;
    
    // Tesseract configuration variable names
    private const string TesseractCharWhitelist = "tessedit_char_whitelist";
    private const string TesseractLoadSystemDawg = "load_system_dawg";
    private const string TesseractLoadFreqDawg = "load_freq_dawg";

    public TesseractOCRService(
        ILogger<TesseractOCRService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        // Get Tesseract data path from configuration, or use default
        _tessDataPath = configuration["Tesseract:DataPath"] ?? "./tessdata";
        
        // Check if tessdata directory exists
        _isAvailable = CheckTesseractAvailability();
        
        if (!_isAvailable)
        {
            _logger.LogWarning(
                "Tesseract OCR not available. Tessdata path: {TessDataPath}. " +
                "Please ensure tessdata folder exists with language files.",
                _tessDataPath);
        }
        else
        {
            _logger.LogInformation("Tesseract OCR initialized successfully with data path: {TessDataPath}", _tessDataPath);
        }
    }

    /// <summary>
    /// Verifica se il servizio OCR è disponibile e configurato correttamente
    /// </summary>
    /// <returns>true se Tesseract è disponibile, false altrimenti</returns>
    /// <remarks>
    /// Scopo: Permettere all'applicazione di fallback gracefully se OCR non disponibile
    /// 
    /// Verifica:
    /// - Esistenza cartella tessdata
    /// - Presenza file linguaggio necessari
    /// 
    /// Output atteso:
    /// - true: OCR pronto all'uso
    /// - false: OCR non disponibile (upload immagini continua ma senza estrazione testo)
    /// </remarks>
    public bool IsAvailable()
    {
        return _isAvailable;
    }

    /// <summary>
    /// Estrae testo da uno stream immagine utilizzando OCR Tesseract
    /// </summary>
    /// <param name="imageStream">Stream contenente i dati dell'immagine</param>
    /// <param name="language">Codice lingua per OCR (default: "eng", italiano: "ita")</param>
    /// <returns>Testo estratto dall'immagine o stringa vuota se OCR non disponibile/fallisce</returns>
    /// <remarks>
    /// Scopo: Rendere documenti scansionati e immagini ricercabili estraendo il testo
    /// 
    /// Processo:
    /// 1. Verifica disponibilità OCR
    /// 2. Conversione stream in byte array
    /// 3. Caricamento immagine con ImageSharp
    /// 4. Salvataggio temporaneo file PNG (Tesseract lavora meglio con file)
    /// 5. Inizializzazione engine Tesseract con lingua specificata
    /// 6. Esecuzione OCR e estrazione testo
    /// 7. Cleanup file temporaneo
    /// 
    /// Lingue supportate (esempi):
    /// - "eng": Inglese
    /// - "ita": Italiano
    /// - "fra": Francese
    /// - "deu": Tedesco
    /// - "spa": Spagnolo
    /// - "eng+ita": Multi-lingua (inglese + italiano)
    /// 
    /// Output atteso:
    /// - Testo estratto dall'immagine (può contenere errori se immagine bassa qualità)
    /// - Stringa vuota se: OCR non disponibile, immagine non valida, errore elaborazione
    /// 
    /// Best practices:
    /// - Utilizzare immagini ad alta risoluzione (300 DPI o superiore)
    /// - Immagini con buon contrasto e illuminazione
    /// - Testo chiaro e leggibile
    /// - Evitare immagini sfocate o inclinate
    /// 
    /// Performance:
    /// - Tipicamente 1-3 secondi per immagine
    /// - Dipende da: dimensione immagine, quantità testo, complessità layout
    /// </remarks>
    public async Task<string> ExtractTextFromImageAsync(Stream imageStream, string language = "eng")
    {
        if (!_isAvailable)
        {
            _logger.LogWarning("Tesseract OCR is not available. Skipping text extraction.");
            return string.Empty;
        }

        try
        {
            _logger.LogInformation("Starting OCR text extraction with language: {Language}", language);

            // Convert image stream to byte array for Tesseract
            byte[] imageBytes;
            if (imageStream.CanSeek)
            {
                imageStream.Position = 0;
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }
            else
            {
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            // Load image and convert to format suitable for Tesseract
            using var image = Image.Load<Rgb24>(imageBytes);
            
            // Create a temporary file for the image (Tesseract works best with files)
            var tempFile = Path.Combine(Path.GetTempPath(), $"ocr_{Path.GetRandomFileName()}.png");
            
            try
            {
                // Save image to temp file
                await image.SaveAsPngAsync(tempFile);

                // Perform OCR using Tesseract
                using var engine = new TesseractEngine(_tessDataPath, language, EngineMode.Default);
                
                // Configure engine for better accuracy
                engine.SetVariable(TesseractCharWhitelist, null); // Allow all characters
                engine.SetVariable(TesseractLoadSystemDawg, "false");
                engine.SetVariable(TesseractLoadFreqDawg, "false");
                
                using var img = Pix.LoadFromFile(tempFile);
                using var page = engine.Process(img);
                
                var text = page.GetText();
                var confidence = page.GetMeanConfidence();
                
                _logger.LogInformation(
                    "OCR completed. Extracted {CharCount} characters with {Confidence:F2}% confidence",
                    text?.Length ?? 0, 
                    confidence * 100);
                
                return text ?? string.Empty;
            }
            finally
            {
                // Clean up temp file
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to delete temporary OCR file: {TempFile}", tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OCR text extraction");
            return string.Empty;
        }
    }

    /// <summary>
    /// Check if Tesseract is available and properly configured
    /// </summary>
    private bool CheckTesseractAvailability()
    {
        try
        {
            // Check if tessdata directory exists
            if (!Directory.Exists(_tessDataPath))
            {
                _logger.LogWarning("Tessdata directory not found at: {TessDataPath}", _tessDataPath);
                return false;
            }

            // Check if at least one language file exists (eng.traineddata)
            var engDataFile = Path.Combine(_tessDataPath, "eng.traineddata");
            if (!File.Exists(engDataFile))
            {
                _logger.LogWarning(
                    "English language data file not found at: {EngDataFile}. " +
                    "Download from: https://github.com/tesseract-ocr/tessdata",
                    engDataFile);
                return false;
            }

            // Try to initialize engine to verify it works
            using var testEngine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Tesseract OCR");
            return false;
        }
    }
}
