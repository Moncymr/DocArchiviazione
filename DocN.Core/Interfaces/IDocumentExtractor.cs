namespace DocN.Core.Interfaces;

/// <summary>
/// Interface for document text extraction
/// </summary>
public interface IDocumentExtractor
{
    /// <summary>
    /// Extract text from a document
    /// </summary>
    /// <param name="stream">Document file stream</param>
    /// <param name="fileName">File name with extension</param>
    /// <returns>Extracted text</returns>
    Task<string> ExtractTextAsync(Stream stream, string fileName);

    /// <summary>
    /// Check if this extractor supports the given file type
    /// </summary>
    /// <param name="contentType">MIME content type</param>
    /// <param name="fileName">File name with extension</param>
    /// <returns>True if supported</returns>
    bool SupportsFileType(string contentType, string fileName);
}

/// <summary>
/// Interface for document metadata extraction
/// </summary>
public interface IMetadataExtractor
{
    /// <summary>
    /// Extract metadata from a document
    /// </summary>
    /// <param name="stream">Document file stream</param>
    /// <param name="fileName">File name</param>
    /// <returns>Dictionary of metadata key-value pairs</returns>
    Task<Dictionary<string, string>> ExtractMetadataAsync(Stream stream, string fileName);
}
