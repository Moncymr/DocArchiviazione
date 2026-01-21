namespace DocN.Core.Interfaces;

/// <summary>
/// Interface for document chunking service
/// </summary>
public interface IChunkingService
{
    /// <summary>
    /// Split document text into chunks for better embedding and search
    /// </summary>
    /// <param name="text">Full document text</param>
    /// <param name="chunkSize">Maximum size of each chunk in characters</param>
    /// <param name="overlap">Overlap between chunks in characters</param>
    /// <returns>List of text chunks</returns>
    List<string> ChunkDocument(string text, int chunkSize = 1000, int overlap = 200);

    /// <summary>
    /// Split document with semantic boundaries (paragraphs, sentences)
    /// </summary>
    /// <param name="text">Full document text</param>
    /// <param name="maxChunkSize">Maximum chunk size</param>
    /// <returns>List of semantically split chunks</returns>
    List<string> ChunkDocumentSemantic(string text, int maxChunkSize = 1000);
}
