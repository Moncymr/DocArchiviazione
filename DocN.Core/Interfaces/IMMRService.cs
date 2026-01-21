namespace DocN.Core.Interfaces;

/// <summary>
/// Service for Maximal Marginal Relevance (MMR) to ensure diversity in search results
/// MMR balances relevance and diversity to avoid returning similar documents
/// </summary>
public interface IMMRService
{
    /// <summary>
    /// Rerank results using MMR to maximize diversity while maintaining relevance
    /// </summary>
    /// <param name="queryVector">The query embedding vector</param>
    /// <param name="candidates">Candidate vectors with their IDs and scores</param>
    /// <param name="topK">Number of results to return</param>
    /// <param name="lambda">Trade-off between relevance (1.0) and diversity (0.0). Default 0.5</param>
    /// <returns>Reranked results with MMR scores</returns>
    Task<List<MMRResult>> RerankWithMMRAsync(
        float[] queryVector,
        List<CandidateVector> candidates,
        int topK,
        double lambda = 0.5);

    /// <summary>
    /// Calculate MMR score for a single candidate
    /// </summary>
    double CalculateMMRScore(
        float[] queryVector,
        float[] candidateVector,
        List<float[]> selectedVectors,
        double lambda = 0.5);
}

/// <summary>
/// Candidate vector for MMR calculation
/// </summary>
public class CandidateVector
{
    public string Id { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public double InitialScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result after MMR reranking
/// </summary>
public class MMRResult
{
    public string Id { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public double InitialScore { get; set; }
    public double MMRScore { get; set; }
    public int Rank { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
