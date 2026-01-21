namespace DocN.Core.Interfaces;

/// <summary>
/// Interface for OCR (Optical Character Recognition) service
/// </summary>
public interface IOCRService
{
    /// <summary>
    /// Extract text from an image using OCR
    /// </summary>
    /// <param name="imageStream">Image stream</param>
    /// <param name="language">Language code (e.g., "eng" for English, "ita" for Italian)</param>
    /// <returns>Extracted text from the image</returns>
    Task<string> ExtractTextFromImageAsync(Stream imageStream, string language = "eng");

    /// <summary>
    /// Check if OCR is available and properly configured
    /// </summary>
    /// <returns>True if OCR is available</returns>
    bool IsAvailable();
}
