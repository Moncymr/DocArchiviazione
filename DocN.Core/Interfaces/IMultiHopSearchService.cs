namespace DocN.Core.Interfaces;

/// <summary>
/// Service for multi-hop search to handle complex queries requiring multiple retrieval steps
/// </summary>
public interface IMultiHopSearchService
{
    /// <summary>
    /// Execute multi-hop search for complex queries
    /// </summary>
    /// <param name="query">Complex query that may require multiple steps</param>
    /// <param name="maxHops">Maximum number of hops to perform (default: 3)</param>
    /// <param name="topKPerHop">Number of results to retrieve per hop (default: 5)</param>
    /// <returns>Final search results after multi-hop reasoning</returns>
    Task<MultiHopSearchResult> SearchAsync(string query, int maxHops = 3, int topKPerHop = 5);
}

/// <summary>
/// Result from multi-hop search including intermediate steps
/// </summary>
public class MultiHopSearchResult
{
    public string OriginalQuery { get; set; } = string.Empty;
    public List<HopStep> Hops { get; set; } = new();
    public List<object> FinalResults { get; set; } = new(); // Final aggregated results
    public int TotalHops { get; set; }
    public double TotalTimeMs { get; set; }
}

/// <summary>
/// Represents a single hop in multi-hop search
/// </summary>
public class HopStep
{
    public int HopNumber { get; set; }
    public string SubQuery { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public List<object> Results { get; set; } = new();
    public double TimeMs { get; set; }
}
