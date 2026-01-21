using Microsoft.Extensions.Logging;

namespace DocN.Data.Utilities;

/// <summary>
/// Helper class for validating embedding dimensions and providing helpful error messages
/// </summary>
public static class EmbeddingValidationHelper
{
    // Constants for common embedding dimensions
    public const int GeminiEmbeddingDimension = 768;
    public const int OpenAIEmbeddingDimension = 1536;
    
    // Supported dimensions for dual VECTOR fields
    public const int SupportedDimension768 = 768;
    public const int SupportedDimension1536 = 1536;
    
    public const int MinimumEmbeddingDimension = 256; // Minimum supported dimension for custom embeddings
    public const int MaximumEmbeddingDimension = 4096; // Maximum reasonable dimension

    /// <summary>
    /// Validates embedding dimensions to ensure they are within acceptable range
    /// </summary>
    /// <param name="embeddingVector">The embedding vector to validate</param>
    /// <param name="logger">Optional logger for recording validation errors</param>
    /// <exception cref="InvalidOperationException">Thrown when embedding dimensions are invalid</exception>
    public static void ValidateEmbeddingDimensions(float[]? embeddingVector, ILogger? logger = null)
    {
        if (embeddingVector != null && embeddingVector.Length > 0)
        {
            var embeddingDimension = embeddingVector.Length;
            
            // Check if dimension is within acceptable range
            // Support flexible dimensions: Gemini (700, 768), OpenAI (1536, 1583, 3072), etc.
            if (embeddingDimension < MinimumEmbeddingDimension || embeddingDimension > MaximumEmbeddingDimension)
            {
                logger?.LogError("Invalid embedding dimension: {Dimension}. Expected between {Min} and {Max}", 
                    embeddingDimension, MinimumEmbeddingDimension, MaximumEmbeddingDimension);
                
                throw new InvalidOperationException(
                    $"Invalid embedding dimension: {embeddingDimension}. " +
                    $"Expected dimension between {MinimumEmbeddingDimension} and {MaximumEmbeddingDimension}. " +
                    $"Common dimensions: 700 (Gemini custom), {GeminiEmbeddingDimension} (Gemini default), " +
                    $"{OpenAIEmbeddingDimension} (OpenAI ada-002), 1583 (OpenAI custom), 3072 (OpenAI large). " +
                    "Please check your AI provider configuration.");
            }
            
            logger?.LogDebug("Validated embedding dimension: {Dimension}", embeddingDimension);
        }
    }
    
    /// <summary>
    /// Checks if an exception message indicates a vector dimension mismatch error
    /// </summary>
    /// <param name="errorMessage">The error message to check</param>
    /// <returns>True if the error appears to be a dimension mismatch, false otherwise</returns>
    public static bool IsVectorDimensionMismatchError(string errorMessage)
    {
        return errorMessage.Contains("dimensioni del vettore", StringComparison.OrdinalIgnoreCase) || 
               errorMessage.Contains("vector", StringComparison.OrdinalIgnoreCase) || 
               errorMessage.Contains("dimension", StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Creates a detailed error message for dimension mismatch errors with actionable solutions
    /// </summary>
    /// <param name="embeddingDimension">The actual embedding dimension that was generated (0 if unknown)</param>
    /// <param name="originalError">The original error message from the database</param>
    /// <returns>A formatted error message with troubleshooting steps</returns>
    public static string CreateDimensionMismatchErrorMessage(int embeddingDimension, string originalError)
    {
        var dimensionInfo = embeddingDimension > 0 
            ? $"📊 Generated embedding dimensions: {embeddingDimension}\n"
            : "";
            
        return $"DATABASE DIMENSION MISMATCH ERROR:\n\n" +
               dimensionInfo +
               $"❌ The system now supports flexible embedding dimensions.\n\n" +
               $"SUPPORTED DIMENSIONS:\n" +
               $"- Gemini (custom): 700 dimensions\n" +
               $"- Gemini (default): {GeminiEmbeddingDimension} dimensions\n" +
               $"- OpenAI ada-002: {OpenAIEmbeddingDimension} dimensions\n" +
               $"- OpenAI (custom): 1583 dimensions\n" +
               $"- OpenAI large: 3072 dimensions\n" +
               $"- Any custom dimension between {MinimumEmbeddingDimension} and {MaximumEmbeddingDimension}\n\n" +
               $"SOLUTION:\n" +
               $"The database now uses flexible JSON storage (nvarchar(max)) that supports any dimension.\n" +
               $"Vectors with different dimensions can coexist in the same database.\n\n" +
               $"If you still see this error:\n" +
               $"1. Ensure your database is using the latest schema (CreateDatabase_Complete_V3.sql)\n" +
               $"2. Check that EmbeddingVector columns use nvarchar(max) type\n" +
               $"3. Verify your AI provider configuration is correct\n\n" +
               $"Original error: {originalError}";
    }
}
