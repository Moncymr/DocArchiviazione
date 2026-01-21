using Microsoft.Extensions.Logging;
using DocN.Core.Interfaces;

namespace DocN.Data.Services;

/// <summary>
/// Implementation of Maximal Marginal Relevance (MMR) for diverse search results
/// MMR formula: MMR = λ * Sim(query, doc) - (1-λ) * max(Sim(doc, selectedDocs))
/// </summary>
public class MMRService : IMMRService
{
    private readonly ILogger<MMRService> _logger;

    public MMRService(ILogger<MMRService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<List<MMRResult>> RerankWithMMRAsync(
        float[] queryVector,
        List<CandidateVector> candidates,
        int topK,
        double lambda = 0.5)
    {
        if (candidates == null || !candidates.Any())
        {
            _logger.LogWarning("No candidates provided for MMR reranking");
            return new List<MMRResult>();
        }

        if (topK <= 0)
        {
            _logger.LogWarning("Invalid topK value: {TopK}", topK);
            return new List<MMRResult>();
        }

        _logger.LogInformation(
            "Starting MMR reranking with {CandidateCount} candidates, topK={TopK}, lambda={Lambda}",
            candidates.Count, topK, lambda);

        var results = new List<MMRResult>();
        var selectedVectors = new List<float[]>();
        var remainingCandidates = new List<CandidateVector>(candidates);

        // Iteratively select documents that maximize MMR score
        for (int i = 0; i < Math.Min(topK, candidates.Count); i++)
        {
            double maxMMRScore = double.MinValue;
            CandidateVector? bestCandidate = null;
            int bestIndex = -1;

            // Find the candidate with the highest MMR score
            for (int j = 0; j < remainingCandidates.Count; j++)
            {
                var candidate = remainingCandidates[j];
                var mmrScore = CalculateMMRScore(queryVector, candidate.Vector, selectedVectors, lambda);

                if (mmrScore > maxMMRScore)
                {
                    maxMMRScore = mmrScore;
                    bestCandidate = candidate;
                    bestIndex = j;
                }
            }

            if (bestCandidate != null && bestIndex >= 0)
            {
                // Add to results
                results.Add(new MMRResult
                {
                    Id = bestCandidate.Id,
                    Vector = bestCandidate.Vector,
                    InitialScore = bestCandidate.InitialScore,
                    MMRScore = maxMMRScore,
                    Rank = i + 1,
                    Metadata = bestCandidate.Metadata
                });

                // Add to selected vectors
                selectedVectors.Add(bestCandidate.Vector);

                // Remove from remaining candidates
                remainingCandidates.RemoveAt(bestIndex);
            }
            else
            {
                break;
            }
        }

        _logger.LogInformation("MMR reranking completed. Selected {ResultCount} documents", results.Count);

        return await Task.FromResult(results);
    }

    /// <inheritdoc/>
    public double CalculateMMRScore(
        float[] queryVector,
        float[] candidateVector,
        List<float[]> selectedVectors,
        double lambda = 0.5)
    {
        // Calculate relevance: similarity to query
        var relevance = CosineSimilarity(queryVector, candidateVector);

        // Calculate diversity: maximum similarity to already selected documents
        var maxSimilarityToSelected = 0.0;
        if (selectedVectors.Any())
        {
            maxSimilarityToSelected = selectedVectors
                .Select(selectedVector => CosineSimilarity(candidateVector, selectedVector))
                .Max();
        }

        // MMR formula: balance between relevance and diversity
        var mmrScore = lambda * relevance - (1 - lambda) * maxSimilarityToSelected;

        return mmrScore;
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private double CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
        {
            _logger.LogWarning(
                "Vector dimension mismatch: {DimA} vs {DimB}",
                vectorA.Length, vectorB.Length);
            return 0;
        }

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}
