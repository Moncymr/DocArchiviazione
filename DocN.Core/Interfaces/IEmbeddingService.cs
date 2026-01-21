namespace DocN.Core.Interfaces;

/// <summary>
/// Interface for embedding generation service using Semantic Kernel
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embedding vector for text using the configured provider (default: Gemini)
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="provider">Optional provider override (Gemini, OpenAI, AzureOpenAI)</param>
    /// <returns>Embedding vector as float array</returns>
    Task<float[]?> GenerateEmbeddingAsync(string text, string? provider = null);

    /// <summary>
    /// Generate embeddings for multiple texts in batch
    /// </summary>
    /// <param name="texts">List of texts to embed</param>
    /// <param name="provider">Optional provider override</param>
    /// <returns>List of embedding vectors</returns>
    Task<List<float[]?>> GenerateBatchEmbeddingsAsync(List<string> texts, string? provider = null);

    /// <summary>
    /// Calculate cosine similarity between two embeddings
    /// </summary>
    /// <param name="embedding1">First embedding vector</param>
    /// <param name="embedding2">Second embedding vector</param>
    /// <returns>Similarity score (0.0 to 1.0)</returns>
    float CalculateSimilarity(float[] embedding1, float[] embedding2);
}
