using Microsoft.EntityFrameworkCore;

namespace DocN.Data.Services;

/// <summary>
/// Service for intelligent autocomplete and search suggestions
/// </summary>
public class SearchSuggestionService : ISearchSuggestionService
{
    private readonly ApplicationDbContext _context;

    public SearchSuggestionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetAutocompleteSuggestionsAsync(string partialQuery, string userId, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(partialQuery) || partialQuery.Length < 2)
        {
            return new List<string>();
        }

        // Get suggestions from user's saved searches
        var savedSearchSuggestions = await _context.SavedSearches
            .Where(s => s.UserId == userId && s.Query.Contains(partialQuery))
            .OrderByDescending(s => s.UseCount)
            .Select(s => s.Query)
            .Take(maxResults)
            .ToListAsync();

        // Get suggestions from document filenames
        var documentNameSuggestions = await _context.Documents
            .Where(d => d.OwnerId == userId && d.FileName.Contains(partialQuery))
            .Select(d => d.FileName.Replace(".pdf", "").Replace(".docx", "").Replace(".txt", ""))
            .Distinct()
            .Take(maxResults - savedSearchSuggestions.Count)
            .ToListAsync();

        // Get suggestions from document categories
        var categorySuggestions = await _context.Documents
            .Where(d => d.OwnerId == userId && 
                   (d.ActualCategory != null && d.ActualCategory.Contains(partialQuery) ||
                    d.SuggestedCategory != null && d.SuggestedCategory.Contains(partialQuery)))
            .Select(d => d.ActualCategory ?? d.SuggestedCategory)
            .Distinct()
            .Take(maxResults - savedSearchSuggestions.Count - documentNameSuggestions.Count)
            .ToListAsync();

        // Combine and deduplicate
        var suggestions = savedSearchSuggestions
            .Concat(documentNameSuggestions)
            .Concat(categorySuggestions.Where(c => c != null).Cast<string>())
            .Distinct()
            .Take(maxResults)
            .ToList();

        return suggestions;
    }

    public async Task<List<string>> GetContextBasedSuggestionsAsync(string query, string userId, int maxResults = 5)
    {
        // Find related searches based on document content
        var relatedDocuments = await _context.Documents
            .Where(d => d.OwnerId == userId && d.ExtractedText.Contains(query))
            .Take(5)
            .ToListAsync();

        var suggestions = new List<string>();

        // Extract common phrases from related documents
        foreach (var doc in relatedDocuments)
        {
            if (!string.IsNullOrEmpty(doc.ActualCategory))
            {
                suggestions.Add($"More documents about {doc.ActualCategory}");
            }
        }

        // Add category-based suggestions
        var categories = await _context.Documents
            .Where(d => d.OwnerId == userId && d.ActualCategory != null)
            .Select(d => d.ActualCategory)
            .Distinct()
            .Take(maxResults)
            .ToListAsync();

        foreach (var category in categories.Where(c => c != null).Cast<string>())
        {
            if (!suggestions.Contains($"Search in {category}"))
            {
                suggestions.Add($"Search in {category}");
            }
        }

        return suggestions.Take(maxResults).ToList();
    }

    public async Task<List<string>> GetPopularQueriesAsync(string userId, int maxResults = 10)
    {
        return await _context.SavedSearches
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UseCount)
            .ThenByDescending(s => s.LastUsedAt)
            .Select(s => s.Query)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task RecordQueryAsync(string query, string userId)
    {
        // Check if query already exists in saved searches
        var existingSearch = await _context.SavedSearches
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Query == query);

        if (existingSearch != null)
        {
            existingSearch.UseCount++;
            existingSearch.LastUsedAt = DateTime.UtcNow;
        }
        else
        {
            // Optionally auto-save frequently used queries
            // This is commented out to avoid auto-creating saved searches
            // Uncomment if you want to track all queries
            /*
            var newSearch = new SavedSearch
            {
                UserId = userId,
                Name = $"Auto: {query.Substring(0, Math.Min(50, query.Length))}",
                Query = query,
                CreatedAt = DateTime.UtcNow,
                UseCount = 1
            };
            _context.SavedSearches.Add(newSearch);
            */
        }

        await _context.SaveChangesAsync();
    }
}
