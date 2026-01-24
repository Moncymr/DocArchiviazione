namespace DocN.Data.Services;

/// <summary>
/// Service interface for intelligent autocomplete and search suggestions
/// </summary>
public interface ISearchSuggestionService
{
    Task<List<string>> GetAutocompleteSuggestionsAsync(string partialQuery, string userId, int maxResults = 10);
    Task<List<string>> GetContextBasedSuggestionsAsync(string query, string userId, int maxResults = 5);
    Task<List<string>> GetPopularQueriesAsync(string userId, int maxResults = 10);
    Task RecordQueryAsync(string query, string userId);
}
