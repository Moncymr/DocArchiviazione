namespace DocN.Data.DTOs;

/// <summary>
/// Filter parameters for enhanced document search
/// </summary>
public class SearchFilterDto
{
    /// <summary>
    /// Document type filters (PDF, Word, Excel, PowerPoint, etc.)
    /// </summary>
    public List<string>? FileTypes { get; set; }
    
    /// <summary>
    /// Date range start for document filtering
    /// </summary>
    public DateTime? DateFrom { get; set; }
    
    /// <summary>
    /// Date range end for document filtering
    /// </summary>
    public DateTime? DateTo { get; set; }
    
    /// <summary>
    /// Minimum file size in MB
    /// </summary>
    public double? MinSizeMB { get; set; }
    
    /// <summary>
    /// Maximum file size in MB
    /// </summary>
    public double? MaxSizeMB { get; set; }
    
    /// <summary>
    /// Filter by document authors/owners
    /// </summary>
    public List<string>? Authors { get; set; }
    
    /// <summary>
    /// Filter by document tags
    /// </summary>
    public List<string>? Tags { get; set; }
    
    /// <summary>
    /// Filter by document status (draft, published, archived)
    /// </summary>
    public string? Status { get; set; }
    
    /// <summary>
    /// Filter by category
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Sort field (relevance, date, name, size)
    /// </summary>
    public string SortBy { get; set; } = "relevance";
    
    /// <summary>
    /// Sort direction (asc, desc)
    /// </summary>
    public string SortDirection { get; set; } = "desc";
    
    /// <summary>
    /// View mode (grid, list)
    /// </summary>
    public string ViewMode { get; set; } = "list";
}

/// <summary>
/// Preset date range options
/// </summary>
public static class DateRangePresets
{
    public const string Last7Days = "last_7_days";
    public const string Last30Days = "last_30_days";
    public const string Last3Months = "last_3_months";
    public const string LastYear = "last_year";
    public const string Custom = "custom";
    
    public static DateTime? GetStartDate(string preset)
    {
        return preset switch
        {
            Last7Days => DateTime.UtcNow.AddDays(-7),
            Last30Days => DateTime.UtcNow.AddDays(-30),
            Last3Months => DateTime.UtcNow.AddMonths(-3),
            LastYear => DateTime.UtcNow.AddYears(-1),
            _ => null
        };
    }
}

/// <summary>
/// Supported file type constants
/// </summary>
public static class FileTypeFilters
{
    public const string PDF = ".pdf";
    public const string Word = ".doc,.docx";
    public const string Excel = ".xls,.xlsx";
    public const string PowerPoint = ".ppt,.pptx";
    public const string Text = ".txt";
    public const string Image = ".jpg,.jpeg,.png,.gif,.bmp";
    public const string All = "*";
}
