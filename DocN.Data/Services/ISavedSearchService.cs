using DocN.Data.Models;

namespace DocN.Data.Services;

/// <summary>
/// Service interface for managing saved searches
/// </summary>
public interface ISavedSearchService
{
    Task<List<SavedSearch>> GetUserSearchesAsync(string userId);
    Task<SavedSearch?> GetSearchAsync(int searchId, string userId);
    Task<SavedSearch> CreateSearchAsync(SavedSearch search);
    Task<SavedSearch> UpdateSearchAsync(SavedSearch search);
    Task DeleteSearchAsync(int searchId, string userId);
    Task RecordSearchUseAsync(int searchId, string userId);
    Task<List<SavedSearch>> GetMostUsedSearchesAsync(string userId, int count = 5);
}
