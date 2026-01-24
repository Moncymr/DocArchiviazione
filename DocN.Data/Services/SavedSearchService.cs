using Microsoft.EntityFrameworkCore;
using DocN.Data.Models;

namespace DocN.Data.Services;

/// <summary>
/// Service for managing saved searches
/// </summary>
public class SavedSearchService : ISavedSearchService
{
    private readonly ApplicationDbContext _context;

    public SavedSearchService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SavedSearch>> GetUserSearchesAsync(string userId)
    {
        return await _context.SavedSearches
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastUsedAt ?? s.CreatedAt)
            .ToListAsync();
    }

    public async Task<SavedSearch?> GetSearchAsync(int searchId, string userId)
    {
        return await _context.SavedSearches
            .FirstOrDefaultAsync(s => s.Id == searchId && s.UserId == userId);
    }

    public async Task<SavedSearch> CreateSearchAsync(SavedSearch search)
    {
        search.CreatedAt = DateTime.UtcNow;
        _context.SavedSearches.Add(search);
        await _context.SaveChangesAsync();
        return search;
    }

    public async Task<SavedSearch> UpdateSearchAsync(SavedSearch search)
    {
        _context.SavedSearches.Update(search);
        await _context.SaveChangesAsync();
        return search;
    }

    public async Task DeleteSearchAsync(int searchId, string userId)
    {
        var search = await GetSearchAsync(searchId, userId);
        if (search != null)
        {
            _context.SavedSearches.Remove(search);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RecordSearchUseAsync(int searchId, string userId)
    {
        var search = await GetSearchAsync(searchId, userId);
        if (search != null)
        {
            search.LastUsedAt = DateTime.UtcNow;
            search.UseCount++;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<SavedSearch>> GetMostUsedSearchesAsync(string userId, int count = 5)
    {
        return await _context.SavedSearches
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UseCount)
            .ThenByDescending(s => s.LastUsedAt)
            .Take(count)
            .ToListAsync();
    }
}
