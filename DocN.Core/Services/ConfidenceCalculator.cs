namespace DocN.Core.Services;

/// <summary>
/// Service for calculating confidence scores for RAG responses
/// </summary>
public class ConfidenceCalculator
{
    /// <summary>
    /// Calculate confidence level based on similarity scores and response quality
    /// </summary>
    /// <param name="averageSimilarityScore">Average similarity score of retrieved chunks (0-1)</param>
    /// <param name="chunkCount">Number of chunks used</param>
    /// <param name="responseLength">Length of generated response</param>
    /// <returns>Confidence score (0-100)</returns>
    public double CalculateConfidence(double averageSimilarityScore, int chunkCount, int responseLength)
    {
        if (averageSimilarityScore < 0 || averageSimilarityScore > 1)
            throw new ArgumentOutOfRangeException(nameof(averageSimilarityScore), "Score must be between 0 and 1");
        
        // Base score from similarity (0-70 points)
        double baseScore = averageSimilarityScore * 70;
        
        // Bonus for multiple source chunks (0-15 points)
        double chunkBonus = Math.Min(chunkCount / 5.0, 1.0) * 15;
        
        // Bonus for substantial response (0-15 points)
        double lengthBonus = Math.Min(responseLength / 500.0, 1.0) * 15;
        
        double totalScore = baseScore + chunkBonus + lengthBonus;
        
        return Math.Min(Math.Round(totalScore, 2), 100);
    }
    
    /// <summary>
    /// Get confidence level category
    /// </summary>
    /// <param name="confidenceScore">Confidence score (0-100)</param>
    /// <returns>Category: High, Medium, or Low</returns>
    public string GetConfidenceLevel(double confidenceScore)
    {
        return confidenceScore switch
        {
            > 80 => "High",
            >= 50 => "Medium",
            _ => "Low"
        };
    }
    
    /// <summary>
    /// Check if response might be a hallucination
    /// </summary>
    /// <param name="confidenceScore">Confidence score (0-100)</param>
    /// <returns>True if possibly hallucinated</returns>
    public bool IsPossibleHallucination(double confidenceScore)
    {
        return confidenceScore < 40;
    }
    
    /// <summary>
    /// Get color for confidence indicator
    /// </summary>
    /// <param name="confidenceScore">Confidence score (0-100)</param>
    /// <returns>Color name: green, yellow, or red</returns>
    public string GetConfidenceColor(double confidenceScore)
    {
        return confidenceScore switch
        {
            > 80 => "green",
            >= 50 => "yellow",
            _ => "red"
        };
    }
}
