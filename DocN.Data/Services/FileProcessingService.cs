using System.Text;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Presentation;
using ClosedXML.Excel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using DocN.Core.Interfaces;

namespace DocN.Data.Services;

/// <summary>
/// Servizio avanzato per l'elaborazione di tutti i tipi di file supportati
/// Gestisce estrazione testo, OCR per immagini, parsing documenti Office, PDF, ecc.
/// </summary>
/// <remarks>
/// Supporta:
/// - PDF (con e senza testo, scansionati con OCR)
/// - Immagini (PNG, JPG, BMP, TIFF) con OCR
/// - Documenti Office (Word, Excel, PowerPoint)
/// - File di testo (TXT, CSV, JSON, XML, HTML)
/// - Email (MSG, EML)
/// </remarks>
public interface IFileProcessingService
{
    /// <summary>
    /// Estrae il testo da qualsiasi file supportato
    /// </summary>
    /// <param name="fileStream">Stream del file</param>
    /// <param name="fileName">Nome del file con estensione</param>
    /// <param name="contentType">Content-Type MIME</param>
    /// <returns>Testo estratto e metadata del file</returns>
    Task<FileProcessingResult> ProcessFileAsync(
        Stream fileStream, 
        string fileName, 
        string contentType);

    /// <summary>
    /// Verifica se il tipo di file è supportato
    /// </summary>
    /// <param name="fileName">Nome file con estensione</param>
    /// <returns>True se supportato</returns>
    bool IsSupportedFileType(string fileName);

    /// <summary>
    /// Ottiene l'icona emoji appropriata per il tipo di file
    /// </summary>
    /// <param name="fileName">Nome file</param>
    /// <returns>Emoji rappresentativa</returns>
    string GetFileIcon(string fileName);

    /// <summary>
    /// Ottiene la lista completa delle estensioni supportate
    /// </summary>
    /// <returns>Array di estensioni (es. ".pdf", ".docx")</returns>
    string[] GetSupportedExtensions();
}

/// <summary>
/// Risultato dell'elaborazione di un file
/// </summary>
public class FileProcessingResult
{
    /// <summary>
    /// Testo estratto dal file
    /// </summary>
    public string ExtractedText { get; set; } = string.Empty;

    /// <summary>
    /// Indica se l'estrazione è riuscita
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Messaggio di errore se l'estrazione è fallita
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Metadata estratti dal file
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Numero di pagine (per PDF e documenti)
    /// </summary>
    public int? PageCount { get; set; }

    /// <summary>
    /// Tabelle estratte (per Excel, PDF con tabelle)
    /// </summary>
    public List<ExtractedTable>? Tables { get; set; }

    /// <summary>
    /// Immagini estratte (per PDF, documenti Office)
    /// </summary>
    public List<ExtractedImage>? Images { get; set; }

    /// <summary>
    /// Entità nominate rilevate (persone, luoghi, date, ecc.)
    /// </summary>
    public List<NamedEntity>? Entities { get; set; }

    /// <summary>
    /// Lingua rilevata del documento
    /// </summary>
    public string? DetectedLanguage { get; set; }

    /// <summary>
    /// Tipo di file rilevato
    /// </summary>
    public FileType FileType { get; set; }
}

/// <summary>
/// Tabella estratta da un documento
/// </summary>
public class ExtractedTable
{
    public string Name { get; set; } = string.Empty;
    public List<List<string>> Rows { get; set; } = new();
    public int RowCount => Rows.Count;
    public int ColumnCount => Rows.FirstOrDefault()?.Count ?? 0;
}

/// <summary>
/// Immagine estratta da un documento
/// </summary>
public class ExtractedImage
{
    public string Name { get; set; } = string.Empty;
    public byte[]? Data { get; set; }
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? OcrText { get; set; } // Testo estratto con OCR se disponibile
}

/// <summary>
/// Entità nominata rilevata nel testo
/// </summary>
public class NamedEntity
{
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Person", "Location", "Date", "Money", etc.
    public double Confidence { get; set; }
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
}

/// <summary>
/// Tipi di file supportati
/// </summary>
public enum FileType
{
    Unknown,
    PDF,
    Word,
    Excel,
    PowerPoint,
    Image,
    Text,
    CSV,
    JSON,
    XML,
    HTML,
    Email,
    Archive
}

/// <summary>
/// Implementazione del servizio di elaborazione file
/// Usa librerie .NET moderne invece di OCX legacy
/// </summary>
public class FileProcessingService : IFileProcessingService
{
    private readonly ILogger<FileProcessingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOCRService _ocrService;

    // Dizionario estensioni supportate -> tipo file
    private static readonly Dictionary<string, FileType> _extensionMap = new()
    {
        // PDF
        { ".pdf", FileType.PDF },
        
        // Microsoft Office
        { ".docx", FileType.Word },
        { ".doc", FileType.Word },
        { ".xlsx", FileType.Excel },
        { ".xls", FileType.Excel },
        { ".pptx", FileType.PowerPoint },
        { ".ppt", FileType.PowerPoint },
        
        // Immagini
        { ".png", FileType.Image },
        { ".jpg", FileType.Image },
        { ".jpeg", FileType.Image },
        { ".bmp", FileType.Image },
        { ".tiff", FileType.Image },
        { ".tif", FileType.Image },
        { ".gif", FileType.Image },
        { ".webp", FileType.Image },
        
        // Testo
        { ".txt", FileType.Text },
        { ".md", FileType.Text },
        { ".csv", FileType.CSV },
        { ".json", FileType.JSON },
        { ".xml", FileType.XML },
        { ".html", FileType.HTML },
        { ".htm", FileType.HTML },
        
        // Email
        { ".msg", FileType.Email },
        { ".eml", FileType.Email },
        
        // Archivi
        { ".zip", FileType.Archive },
        { ".rar", FileType.Archive },
        { ".7z", FileType.Archive }
    };

    // Dizionario tipo file -> icona
    private static readonly Dictionary<FileType, string> _fileIcons = new()
    {
        { FileType.PDF, "📕" },
        { FileType.Word, "📘" },
        { FileType.Excel, "📗" },
        { FileType.PowerPoint, "📙" },
        { FileType.Image, "🖼️" },
        { FileType.Text, "📄" },
        { FileType.CSV, "📊" },
        { FileType.JSON, "📋" },
        { FileType.XML, "📋" },
        { FileType.HTML, "🌐" },
        { FileType.Email, "📧" },
        { FileType.Archive, "📦" },
        { FileType.Unknown, "📎" }
    };

    public FileProcessingService(
        ILogger<FileProcessingService> logger,
        IConfiguration configuration,
        IOCRService ocrService)
    {
        _logger = logger;
        _configuration = configuration;
        _ocrService = ocrService;
    }

    /// <inheritdoc/>
    public async Task<FileProcessingResult> ProcessFileAsync(
        Stream fileStream, 
        string fileName, 
        string contentType)
    {
        var result = new FileProcessingResult
        {
            FileType = GetFileType(fileName)
        };

        try
        {
            _logger.LogInformation(
                "Inizio elaborazione file: {FileName}, Tipo: {FileType}",
                fileName, result.FileType);

            // Elabora in base al tipo di file
            switch (result.FileType)
            {
                case FileType.PDF:
                    await ProcessPdfAsync(fileStream, result);
                    break;
                
                case FileType.Word:
                    await ProcessWordAsync(fileStream, result);
                    break;
                
                case FileType.Excel:
                    await ProcessExcelAsync(fileStream, result);
                    break;
                
                case FileType.PowerPoint:
                    await ProcessPowerPointAsync(fileStream, result);
                    break;
                
                case FileType.Image:
                    await ProcessImageAsync(fileStream, fileName, result);
                    break;
                
                case FileType.Text:
                case FileType.CSV:
                case FileType.JSON:
                case FileType.XML:
                case FileType.HTML:
                    await ProcessTextFileAsync(fileStream, result);
                    break;
                
                case FileType.Email:
                    await ProcessEmailAsync(fileStream, result);
                    break;
                
                default:
                    result.Success = false;
                    result.ErrorMessage = $"Tipo di file non supportato: {result.FileType}";
                    _logger.LogWarning("Tipo file non supportato: {FileType}", result.FileType);
                    break;
            }

            if (result.Success)
            {
                _logger.LogInformation(
                    "File elaborato con successo: {FileName}, Caratteri estratti: {CharCount}",
                    fileName, result.ExtractedText?.Length ?? 0);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante elaborazione file: {FileName}", fileName);
            
            result.Success = false;
            result.ErrorMessage = $"Errore durante elaborazione: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Elabora un file PDF
    /// Supporta PDF con testo, estrazione metadata, conteggio pagine
    /// </summary>
    private async Task ProcessPdfAsync(Stream stream, FileProcessingResult result)
    {
        try
        {
            _logger.LogInformation("Elaborazione PDF in corso...");

            // Create a temporary file to work with iText7 (it needs seekable stream)
            // Use secure random filename to prevent predictable temporary file attacks
            var tempFile = Path.Combine(Path.GetTempPath(), $"pdf_{Path.GetRandomFileName()}.tmp");
            try
            {
                // Copy stream to temp file
                using (var fileStream = File.Create(tempFile))
                {
                    await stream.CopyToAsync(fileStream);
                }

                // Open PDF document
                using var pdfReader = new PdfReader(tempFile);
                using var pdfDocument = new PdfDocument(pdfReader);
                
                var textBuilder = new StringBuilder();
                int pageCount = pdfDocument.GetNumberOfPages();
                
                _logger.LogInformation("PDF has {PageCount} pages", pageCount);

                // Extract text from each page
                for (int i = 1; i <= pageCount; i++)
                {
                    try
                    {
                        var page = pdfDocument.GetPage(i);
                        var strategy = new LocationTextExtractionStrategy();
                        var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                        
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            textBuilder.AppendLine($"--- Page {i} ---");
                            textBuilder.AppendLine(pageText);
                            textBuilder.AppendLine();
                        }
                    }
                    catch (Exception pageEx)
                    {
                        _logger.LogWarning(pageEx, "Errore nell'estrazione testo dalla pagina {PageNumber}", i);
                        textBuilder.AppendLine($"[Error extracting text from page {i}]");
                    }
                }

                result.ExtractedText = textBuilder.ToString();
                result.PageCount = pageCount;
                
                // Extract metadata
                var info = pdfDocument.GetDocumentInfo();
                if (info != null)
                {
                    if (!string.IsNullOrEmpty(info.GetTitle()))
                        result.Metadata["Title"] = info.GetTitle();
                    if (!string.IsNullOrEmpty(info.GetAuthor()))
                        result.Metadata["Author"] = info.GetAuthor();
                    if (!string.IsNullOrEmpty(info.GetSubject()))
                        result.Metadata["Subject"] = info.GetSubject();
                    if (!string.IsNullOrEmpty(info.GetKeywords()))
                        result.Metadata["Keywords"] = info.GetKeywords();
                    if (!string.IsNullOrEmpty(info.GetCreator()))
                        result.Metadata["Creator"] = info.GetCreator();
                    if (!string.IsNullOrEmpty(info.GetProducer()))
                        result.Metadata["Producer"] = info.GetProducer();
                }

                result.Success = true;
                _logger.LogInformation("PDF elaborato con successo: {Pages} pagine, {Chars} caratteri estratti", 
                    pageCount, result.ExtractedText.Length);
            }
            finally
            {
                // Clean up temp file
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Impossibile eliminare il file temporaneo: {TempFile}", tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore elaborazione PDF");
            result.Success = false;
            result.ErrorMessage = $"Errore durante l'elaborazione del PDF: {ex.Message}";
        }
    }

    /// <summary>
    /// Elabora un documento Word (.docx, .doc)
    /// </summary>
    private async Task ProcessWordAsync(Stream stream, FileProcessingResult result)
    {
        try
        {
            _logger.LogInformation("Elaborazione Word in corso...");

            // Create a temporary file for OpenXml (it needs seekable stream)
            var tempFile = Path.Combine(Path.GetTempPath(), $"word_{Path.GetRandomFileName()}.tmp");
            try
            {
                // Copy stream to temp file
                using (var fileStream = File.Create(tempFile))
                {
                    await stream.CopyToAsync(fileStream);
                }

                // Open Word document
                using var wordDocument = WordprocessingDocument.Open(tempFile, false);
                var body = wordDocument.MainDocumentPart?.Document?.Body;
                
                if (body == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Documento Word non valido o vuoto";
                    return;
                }

                var textBuilder = new StringBuilder();
                
                // Extract all text from paragraphs
                foreach (var paragraph in body.Descendants<Paragraph>())
                {
                    var paragraphText = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(paragraphText))
                    {
                        textBuilder.AppendLine(paragraphText);
                    }
                }
                
                // Extract text from tables
                foreach (var table in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>())
                {
                    textBuilder.AppendLine("\n[TABLE]");
                    foreach (var row in table.Descendants<TableRow>())
                    {
                        var rowText = string.Join(" | ", 
                            row.Descendants<TableCell>().Select(cell => cell.InnerText.Trim()));
                        if (!string.IsNullOrWhiteSpace(rowText))
                        {
                            textBuilder.AppendLine(rowText);
                        }
                    }
                    textBuilder.AppendLine("[/TABLE]\n");
                }

                result.ExtractedText = textBuilder.ToString();
                
                // Extract metadata
                var coreProps = wordDocument.PackageProperties;
                if (coreProps != null)
                {
                    if (!string.IsNullOrEmpty(coreProps.Title))
                        result.Metadata["Title"] = coreProps.Title;
                    if (!string.IsNullOrEmpty(coreProps.Creator))
                        result.Metadata["Author"] = coreProps.Creator;
                    if (!string.IsNullOrEmpty(coreProps.Subject))
                        result.Metadata["Subject"] = coreProps.Subject;
                    if (!string.IsNullOrEmpty(coreProps.Keywords))
                        result.Metadata["Keywords"] = coreProps.Keywords;
                    if (coreProps.Created.HasValue)
                        result.Metadata["CreatedDate"] = coreProps.Created.Value.ToString("yyyy-MM-dd");
                    if (coreProps.Modified.HasValue)
                        result.Metadata["ModifiedDate"] = coreProps.Modified.Value.ToString("yyyy-MM-dd");
                }

                result.Success = true;
                _logger.LogInformation("Word elaborato: {Chars} caratteri estratti", result.ExtractedText.Length);
            }
            finally
            {
                // Clean up temp file
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Impossibile eliminare il file temporaneo: {TempFile}", tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore elaborazione Word");
            result.Success = false;
            result.ErrorMessage = $"Errore durante l'elaborazione del documento Word: {ex.Message}";
        }
    }

    /// <summary>
    /// Elabora un file Excel (.xlsx, .xls)
    /// </summary>
    private async Task ProcessExcelAsync(Stream stream, FileProcessingResult result)
    {
        try
        {
            _logger.LogInformation("Elaborazione Excel in corso...");

            // Create a temporary file for ClosedXML (it needs seekable stream)
            var tempFile = Path.Combine(Path.GetTempPath(), $"excel_{Path.GetRandomFileName()}.tmp");
            try
            {
                // Copy stream to temp file
                using (var fileStream = File.Create(tempFile))
                {
                    await stream.CopyToAsync(fileStream);
                }

                // Open Excel workbook
                using var workbook = new XLWorkbook(tempFile);
                var textBuilder = new StringBuilder();
                result.Tables = new List<ExtractedTable>();
                
                // Process each worksheet
                foreach (var worksheet in workbook.Worksheets)
                {
                    textBuilder.AppendLine($"\n=== Sheet: {worksheet.Name} ===\n");
                    
                    var usedRange = worksheet.RangeUsed();
                    if (usedRange == null)
                    {
                        textBuilder.AppendLine("[Empty sheet]");
                        continue;
                    }

                    var table = new ExtractedTable
                    {
                        Name = worksheet.Name
                    };

                    // Extract data row by row
                    foreach (var row in usedRange.Rows())
                    {
                        var rowData = new List<string>();
                        var rowText = new StringBuilder();
                        
                        foreach (var cell in row.Cells())
                        {
                            // Use GetFormattedString to preserve Excel cell formatting (dates, numbers, etc.)
                            var cellValue = cell.GetFormattedString();
                            rowData.Add(cellValue);
                            rowText.Append(cellValue);
                            rowText.Append("\t");
                        }
                        
                        table.Rows.Add(rowData);
                        textBuilder.AppendLine(rowText.ToString().TrimEnd());
                    }
                    
                    result.Tables.Add(table);
                    textBuilder.AppendLine();
                }

                result.ExtractedText = textBuilder.ToString();
                
                // Extract metadata
                var props = workbook.Properties;
                if (props != null)
                {
                    if (!string.IsNullOrEmpty(props.Title))
                        result.Metadata["Title"] = props.Title;
                    if (!string.IsNullOrEmpty(props.Author))
                        result.Metadata["Author"] = props.Author;
                    if (!string.IsNullOrEmpty(props.Subject))
                        result.Metadata["Subject"] = props.Subject;
                    if (!string.IsNullOrEmpty(props.Keywords))
                        result.Metadata["Keywords"] = props.Keywords;
                    if (!string.IsNullOrEmpty(props.Company))
                        result.Metadata["Company"] = props.Company;
                }

                result.Success = true;
                _logger.LogInformation("Excel elaborato: {Sheets} fogli, {Tables} tabelle, {Chars} caratteri", 
                    workbook.Worksheets.Count, result.Tables.Count, result.ExtractedText.Length);
            }
            finally
            {
                // Clean up temp file
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Impossibile eliminare il file temporaneo: {TempFile}", tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore elaborazione Excel");
            result.Success = false;
            result.ErrorMessage = $"Errore durante l'elaborazione del file Excel: {ex.Message}";
        }
    }

    /// <summary>
    /// Elabora una presentazione PowerPoint (.pptx, .ppt)
    /// </summary>
    private async Task ProcessPowerPointAsync(Stream stream, FileProcessingResult result)
    {
        try
        {
            _logger.LogInformation("Elaborazione PowerPoint in corso...");

            // Create a temporary file for OpenXml (it needs seekable stream)
            var tempFile = Path.Combine(Path.GetTempPath(), $"ppt_{Path.GetRandomFileName()}.tmp");
            try
            {
                // Copy stream to temp file
                using (var fileStream = File.Create(tempFile))
                {
                    await stream.CopyToAsync(fileStream);
                }

                // Open PowerPoint presentation
                using var presentation = PresentationDocument.Open(tempFile, false);
                var presentationPart = presentation.PresentationPart;
                
                if (presentationPart == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Presentazione PowerPoint non valida o vuota";
                    return;
                }

                var textBuilder = new StringBuilder();
                var slidesPart = presentationPart.SlideParts;
                int slideNumber = 1;

                // Extract text from each slide
                foreach (var slidePart in slidesPart)
                {
                    textBuilder.AppendLine($"\n=== Slide {slideNumber} ===\n");
                    
                    // Extract text from all text-containing elements
                    var texts = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                    foreach (var text in texts)
                    {
                        if (!string.IsNullOrWhiteSpace(text.Text))
                        {
                            textBuilder.AppendLine(text.Text);
                        }
                    }
                    
                    // Extract notes if available
                    if (slidePart.NotesSlidePart != null)
                    {
                        var noteTexts = slidePart.NotesSlidePart.NotesSlide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                        var notes = string.Join(" ", noteTexts.Select(t => t.Text?.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)));
                        if (!string.IsNullOrWhiteSpace(notes))
                        {
                            textBuilder.AppendLine($"\n[Notes: {notes}]");
                        }
                    }
                    
                    textBuilder.AppendLine();
                    slideNumber++;
                }

                result.ExtractedText = textBuilder.ToString();
                result.PageCount = slideNumber - 1;
                
                // Extract metadata
                var coreProps = presentation.PackageProperties;
                if (coreProps != null)
                {
                    if (!string.IsNullOrEmpty(coreProps.Title))
                        result.Metadata["Title"] = coreProps.Title;
                    if (!string.IsNullOrEmpty(coreProps.Creator))
                        result.Metadata["Author"] = coreProps.Creator;
                    if (!string.IsNullOrEmpty(coreProps.Subject))
                        result.Metadata["Subject"] = coreProps.Subject;
                    if (coreProps.Created.HasValue)
                        result.Metadata["CreatedDate"] = coreProps.Created.Value.ToString("yyyy-MM-dd");
                }

                result.Success = true;
                _logger.LogInformation("PowerPoint elaborato: {Slides} slide, {Chars} caratteri", 
                    result.PageCount, result.ExtractedText.Length);
            }
            finally
            {
                // Clean up temp file
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Impossibile eliminare il file temporaneo: {TempFile}", tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore elaborazione PowerPoint");
            result.Success = false;
            result.ErrorMessage = $"Errore durante l'elaborazione della presentazione: {ex.Message}";
        }
    }

    /// <summary>
    /// Elabora un'immagine
    /// Estrae metadata EXIF e informazioni di base (dimensioni, formato, ecc.)
    /// </summary>
    private async Task ProcessImageAsync(Stream stream, string fileName, FileProcessingResult result)
    {
        try
        {
            _logger.LogInformation("Elaborazione immagine: {FileName}", fileName);

            // Reset stream position if possible, otherwise copy to memory stream
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
            else
            {
                // For non-seekable streams, copy to memory stream
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                stream = memoryStream;
            }

            // Load image directly from stream to extract metadata
            using var image = await Image.LoadAsync(stream);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            // Build extracted text with image information
            var textBuilder = new StringBuilder();
            textBuilder.AppendLine($"[Immagine: {fileName}]");
            textBuilder.AppendLine($"Formato: {image.Metadata.DecodedImageFormat?.Name ?? extension}");
            textBuilder.AppendLine($"Dimensioni: {image.Width} x {image.Height} pixels");
            textBuilder.AppendLine($"Proporzioni: {(double)image.Width / image.Height:F2}:1");
            
            // Extract basic metadata
            result.Metadata["OriginalFileName"] = fileName;
            result.Metadata["FileType"] = "Image";
            result.Metadata["Extension"] = extension;
            result.Metadata["Format"] = image.Metadata.DecodedImageFormat?.Name ?? "Unknown";
            result.Metadata["Width"] = image.Width.ToString();
            result.Metadata["Height"] = image.Height.ToString();
            result.Metadata["AspectRatio"] = $"{(double)image.Width / image.Height:F2}:1";
            result.Metadata["HorizontalResolution"] = image.Metadata.HorizontalResolution.ToString();
            result.Metadata["VerticalResolution"] = image.Metadata.VerticalResolution.ToString();
                
                // Extract EXIF metadata if available
                if (image.Metadata.ExifProfile != null)
                {
                    var exif = image.Metadata.ExifProfile;
                    textBuilder.AppendLine("\n[Metadati EXIF]");
                    
                    // Camera information
                    if (exif.TryGetValue(ExifTag<string>.Make, out var makeValue) && makeValue?.Value != null)
                    {
                        var make = makeValue.Value;
                        var model = "";
                        if (exif.TryGetValue(ExifTag<string>.Model, out var modelValue) && modelValue?.Value != null)
                        {
                            model = modelValue.Value;
                        }
                        
                        if (!string.IsNullOrEmpty(make) || !string.IsNullOrEmpty(model))
                        {
                            var camera = $"{make} {model}".Trim();
                            textBuilder.AppendLine($"Fotocamera: {camera}");
                            result.Metadata["Camera"] = camera;
                        }
                    }
                    
                    // Date taken
                    string? dateTaken = null;
                    if (exif.TryGetValue(ExifTag<string>.DateTimeOriginal, out var dateTimeOrigValue) && dateTimeOrigValue?.Value != null)
                    {
                        dateTaken = dateTimeOrigValue.Value;
                    }
                    else if (exif.TryGetValue(ExifTag<string>.DateTime, out var dateTimeValue) && dateTimeValue?.Value != null)
                    {
                        dateTaken = dateTimeValue.Value;
                    }
                    
                    if (dateTaken != null)
                    {
                        textBuilder.AppendLine($"Data scatto: {dateTaken}");
                        result.Metadata["DateTaken"] = dateTaken;
                    }
                    
                    // Software
                    if (exif.TryGetValue(ExifTag<string>.Software, out var softwareValue) && softwareValue?.Value != null)
                    {
                        textBuilder.AppendLine($"Software: {softwareValue.Value}");
                        result.Metadata["Software"] = softwareValue.Value;
                    }
                    
                    // GPS coordinates
                    if (exif.TryGetValue(ExifTag<Rational[]>.GPSLatitude, out var gpsLatValue) &&
                        exif.TryGetValue(ExifTag<string>.GPSLatitudeRef, out var gpsLatRefValue) &&
                        exif.TryGetValue(ExifTag<Rational[]>.GPSLongitude, out var gpsLongValue) &&
                        exif.TryGetValue(ExifTag<string>.GPSLongitudeRef, out var gpsLongRefValue) &&
                        gpsLatValue?.Value != null && gpsLongValue?.Value != null)
                    {
                        try
                        {
                            var gpsLat = gpsLatValue.Value;
                            var gpsLatRef = gpsLatRefValue?.Value;
                            var gpsLong = gpsLongValue.Value;
                            var gpsLongRef = gpsLongRefValue?.Value;
                            
                            var lat = ConvertGpsToDecimal(gpsLat, gpsLatRef);
                            var lng = ConvertGpsToDecimal(gpsLong, gpsLongRef);
                            textBuilder.AppendLine($"Coordinate GPS: {lat:F6}, {lng:F6}");
                            result.Metadata["GPS_Latitude"] = lat.ToString("F6");
                            result.Metadata["GPS_Longitude"] = lng.ToString("F6");
                        }
                        catch (Exception gpsEx)
                        {
                            _logger.LogWarning(gpsEx, "Failed to parse GPS coordinates for {FileName}", fileName);
                        }
                    }
                    
                    // Camera settings - helper to extract with logging
                    void TryExtractExifValue<T>(ExifTag<T> tag, string displayName, Action<T> onSuccess) where T : notnull
                    {
                        try
                        {
                            if (exif.TryGetValue(tag, out var value) && value != null && value.Value != null)
                            {
                                onSuccess(value.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to extract EXIF {Field} for {FileName}", displayName, fileName);
                        }
                    }
                    
                    // ISO
                    TryExtractExifValue(ExifTag<ushort[]>.ISOSpeedRatings, "ISO", iso =>
                    {
                        if (iso.Length > 0)
                        {
                            textBuilder.AppendLine($"ISO: {iso[0]}");
                            result.Metadata["ISO"] = iso[0].ToString();
                        }
                    });
                    
                    // F-Number (Aperture)
                    TryExtractExifValue(ExifTag<Rational>.FNumber, "FNumber", fNumber =>
                    {
                        textBuilder.AppendLine($"Apertura: f/{fNumber.ToDouble():F1}");
                        result.Metadata["Aperture"] = $"f/{fNumber.ToDouble():F1}";
                    });
                    
                    // Exposure Time
                    TryExtractExifValue(ExifTag<Rational>.ExposureTime, "ExposureTime", exposureTime =>
                    {
                        textBuilder.AppendLine($"Tempo esposizione: {exposureTime}s");
                        result.Metadata["ExposureTime"] = $"{exposureTime}s";
                    });
                    
                    // Focal Length
                    TryExtractExifValue(ExifTag<Rational>.FocalLength, "FocalLength", focalLength =>
                    {
                        textBuilder.AppendLine($"Lunghezza focale: {focalLength.ToDouble():F1}mm");
                        result.Metadata["FocalLength"] = $"{focalLength.ToDouble():F1}mm";
                    });
                    
                    _logger.LogInformation("EXIF data estratti per {FileName}", fileName);
                }
                else
                {
                    textBuilder.AppendLine("\nNessun metadato EXIF disponibile.");
                    _logger.LogInformation("Nessun EXIF data disponibile per {FileName}", fileName);
                }
                
                // *** OCR Text Extraction ***
                // Extract text from the image using Tesseract OCR
                if (_ocrService.IsAvailable())
                {
                    _logger.LogInformation("Attempting OCR text extraction from image: {FileName}", fileName);
                    textBuilder.AppendLine("\n[Testo estratto con OCR]");
                    
                    try
                    {
                        // Get language setting from configuration (default to Italian)
                        var ocrLanguage = _configuration["Tesseract:Language"] ?? "ita";
                        string ocrText;
                        
                        // Create a seekable stream for OCR processing
                        if (stream.CanSeek)
                        {
                            stream.Position = 0;
                            ocrText = await _ocrService.ExtractTextFromImageAsync(stream, ocrLanguage);
                        }
                        else
                        {
                            // If stream is not seekable, create a new one from the image
                            using var memStream = new MemoryStream();
                            await image.SaveAsPngAsync(memStream);
                            memStream.Position = 0;
                            ocrText = await _ocrService.ExtractTextFromImageAsync(memStream, ocrLanguage);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(ocrText))
                        {
                            textBuilder.AppendLine(ocrText);
                            result.Metadata["OCR_Enabled"] = "true";
                            result.Metadata["OCR_Language"] = ocrLanguage;
                            result.Metadata["OCR_CharCount"] = ocrText.Length.ToString();
                            _logger.LogInformation(
                                "OCR text extracted successfully from {FileName}: {CharCount} characters", 
                                fileName, 
                                ocrText.Length);
                        }
                        else
                        {
                            textBuilder.AppendLine("Nessun testo rilevato nell'immagine.");
                            result.Metadata["OCR_Enabled"] = "true";
                            result.Metadata["OCR_Result"] = "No text detected";
                            _logger.LogInformation("No text detected in image: {FileName}", fileName);
                        }
                    }
                    catch (Exception ocrEx)
                    {
                        _logger.LogWarning(ocrEx, "OCR extraction failed for {FileName}", fileName);
                        textBuilder.AppendLine("Errore durante l'estrazione OCR.");
                        result.Metadata["OCR_Enabled"] = "true";
                        result.Metadata["OCR_Error"] = ocrEx.Message;
                    }
                }
                else
                {
                    textBuilder.AppendLine("\nOCR non disponibile. Installare Tesseract e i dati linguistici per abilitare l'estrazione di testo dalle immagini.");
                    result.Metadata["OCR_Enabled"] = "false";
                    _logger.LogInformation("OCR not available for {FileName}", fileName);
                }
                
                result.ExtractedText = textBuilder.ToString();
                result.Success = true;
                
                _logger.LogInformation("Immagine elaborata con successo: {FileName} ({Width}x{Height})", 
                    fileName, image.Width, image.Height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore elaborazione immagine: {FileName}", fileName);
            result.Success = false;
            result.ErrorMessage = $"Errore durante l'elaborazione dell'immagine: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Converte coordinate GPS da formato EXIF a decimale
    /// </summary>
    private double ConvertGpsToDecimal(Rational[] coordinate, string? reference)
    {
        if (coordinate == null || coordinate.Length < 3)
            return 0;
            
        var degrees = coordinate[0].ToDouble();
        var minutes = coordinate[1].ToDouble();
        var seconds = coordinate[2].ToDouble();
        
        var result = degrees + (minutes / 60.0) + (seconds / 3600.0);
        
        // Apply negative sign for South/West
        if (reference != null && (reference.Equals("S", StringComparison.OrdinalIgnoreCase) || 
                                  reference.Equals("W", StringComparison.OrdinalIgnoreCase)))
            result = -result;
            
        return result;
    }

    /// <summary>
    /// Elabora file di testo semplici
    /// </summary>
    private async Task ProcessTextFileAsync(Stream stream, FileProcessingResult result)
    {
        try
        {
            _logger.LogInformation("Elaborazione file di testo...");

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync();
            
            // Special handling for XML files - format for better readability
            if (result.FileType == FileType.XML && !string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    // Use XmlReader with secure settings to prevent XXE attacks
                    var settings = new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null,
                        MaxCharactersFromEntities = 1024,
                        MaxCharactersInDocument = 10_000_000
                    };
                    
                    using var stringReader = new StringReader(content);
                    using var xmlReader = XmlReader.Create(stringReader, settings);
                    
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(xmlReader);
                    
                    // Format XML with indentation
                    using var stringWriter = new StringWriter();
                    using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "  ",
                        NewLineChars = "\n",
                        OmitXmlDeclaration = false
                    });
                    xmlDoc.Save(xmlWriter);
                    result.ExtractedText = stringWriter.ToString();
                    
                    // Extract XML metadata
                    result.Metadata["RootElement"] = xmlDoc.DocumentElement?.Name ?? "Unknown";
                }
                catch
                {
                    // If XML parsing fails, use raw content
                    result.ExtractedText = content;
                    result.Metadata["XMLParsingNote"] = "XML non valido, contenuto raw estratto";
                }
            }
            else
            {
                result.ExtractedText = content;
            }
            
            // Set encoding metadata for all text files
            result.Metadata["Encoding"] = reader.CurrentEncoding.WebName;
            result.Success = true;
            
            _logger.LogInformation("File testo elaborato: {Chars} caratteri, Tipo: {FileType}", 
                result.ExtractedText.Length, result.FileType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore elaborazione file testo");
            result.Success = false;
            result.ErrorMessage = $"Errore durante l'elaborazione del file di testo: {ex.Message}";
        }
    }

    /// <summary>
    /// Elabora email (.msg, .eml)
    /// </summary>
    private async Task ProcessEmailAsync(Stream stream, FileProcessingResult result)
    {
        try
        {
            // TODO: Implementare con MsgReader o MailKit
            
            _logger.LogInformation("Elaborazione email...");

            using var reader = new StreamReader(stream);
            var rawContent = await reader.ReadToEndAsync();
            
            // Estrai campi email
            var textBuilder = new StringBuilder();
            textBuilder.AppendLine("=== EMAIL ===");
            // TODO: Parse Subject, From, To, Date, Body, Attachments
            textBuilder.AppendLine(rawContent);
            
            result.ExtractedText = textBuilder.ToString();
            result.Success = true;
            
            // TODO: Implementare
            // - Parsing header email
            // - Estrazione corpo HTML/testo
            // - Estrazione allegati
            // - Supporto .msg (Outlook) e .eml (standard)
            
            _logger.LogInformation("Email elaborata");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore elaborazione email");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
    }

    /// <inheritdoc/>
    public bool IsSupportedFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return _extensionMap.ContainsKey(extension);
    }

    /// <inheritdoc/>
    public string GetFileIcon(string fileName)
    {
        var fileType = GetFileType(fileName);
        return _fileIcons.GetValueOrDefault(fileType, "📎");
    }

    /// <inheritdoc/>
    public string[] GetSupportedExtensions()
    {
        return _extensionMap.Keys.ToArray();
    }

    /// <summary>
    /// Determina il tipo di file dall'estensione
    /// </summary>
    private FileType GetFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return _extensionMap.GetValueOrDefault(extension, FileType.Unknown);
    }
}
