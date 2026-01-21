using DocN.Data.Models;
using System.Text;

namespace DocN.Data.Services;

/// <summary>
/// Generates natural language answers from document statistics
/// </summary>
public interface IStatisticalAnswerGenerator
{
    /// <summary>
    /// Generate a natural language answer to a statistical query
    /// </summary>
    /// <param name="query">The user's query</param>
    /// <param name="statistics">Document statistics from the database</param>
    /// <returns>Natural language answer</returns>
    Task<string> GenerateAnswerAsync(string query, DocumentStatistics statistics);
}

/// <summary>
/// Generates natural language answers from document statistics without AI
/// Uses template-based approach for deterministic, fast responses
/// </summary>
public class StatisticalAnswerGenerator : IStatisticalAnswerGenerator
{
    public Task<string> GenerateAnswerAsync(string query, DocumentStatistics statistics)
    {
        var normalizedQuery = query.ToLowerInvariant();
        var answer = new StringBuilder();

        // Detect language (simple heuristic)
        var isItalian = ContainsItalianKeywords(normalizedQuery);

        // Total documents query
        if (ContainsAny(normalizedQuery, "how many", "quanti", "quante", "count", "total", "totale", "numero"))
        {
            if (ContainsAny(normalizedQuery, "pdf", "pdfs"))
            {
                var pdfCount = statistics.DocumentsByType.ContainsKey("application/pdf") 
                    ? statistics.DocumentsByType["application/pdf"] 
                    : 0;
                
                answer.Append(isItalian 
                    ? $"Nel sistema ci sono **{pdfCount} documenti PDF**" 
                    : $"There are **{pdfCount} PDF documents** in the system");
                
                if (statistics.TotalDocuments > pdfCount)
                {
                    answer.Append(isItalian 
                        ? $" su un totale di {statistics.TotalDocuments} documenti." 
                        : $" out of {statistics.TotalDocuments} total documents.");
                }
                else
                {
                    answer.Append(".");
                }
            }
            else if (ContainsAny(normalizedQuery, "documents", "documenti", "files", "file"))
            {
                answer.Append(isItalian 
                    ? $"Nel sistema ci sono **{statistics.TotalDocuments} documenti** in totale." 
                    : $"There are **{statistics.TotalDocuments} documents** in the system.");
                
                // Add breakdown by type
                if (statistics.DocumentsByType.Any())
                {
                    answer.Append(isItalian ? "\n\n**Suddivisione per tipo:**\n" : "\n\n**Breakdown by type:**\n");
                    foreach (var typeGroup in statistics.DocumentsByType.OrderByDescending(x => x.Value).Take(5))
                    {
                        var typeName = GetFriendlyTypeName(typeGroup.Key);
                        answer.Append($"- {typeName}: {typeGroup.Value}\n");
                    }
                }
            }
            else
            {
                // Generic count question
                answer.Append(isItalian 
                    ? $"Nel sistema ci sono **{statistics.TotalDocuments} documenti** in totale." 
                    : $"There are **{statistics.TotalDocuments} documents** in the system.");
            }
        }
        // Categories query
        else if (ContainsAny(normalizedQuery, "categories", "categorie", "category", "categoria"))
        {
            if (statistics.DocumentsByCategory.Any())
            {
                answer.Append(isItalian 
                    ? $"Ci sono **{statistics.DocumentsByCategory.Count} categorie** nel sistema:\n\n" 
                    : $"There are **{statistics.DocumentsByCategory.Count} categories** in the system:\n\n");
                
                foreach (var category in statistics.DocumentsByCategory.OrderByDescending(x => x.Value))
                {
                    answer.Append($"- **{category.Key}**: {category.Value} ");
                    answer.Append(isItalian ? "documenti\n" : "documents\n");
                }
            }
            else
            {
                answer.Append(isItalian 
                    ? "Non ci sono categorie definite nel sistema." 
                    : "There are no categories defined in the system.");
            }
        }
        // Types/extensions query
        else if (ContainsAny(normalizedQuery, "types", "tipi", "extensions", "estensioni", "formats", "formati"))
        {
            if (statistics.DocumentsByType.Any())
            {
                answer.Append(isItalian 
                    ? $"Ci sono **{statistics.DocumentsByType.Count} tipi di file** nel sistema:\n\n" 
                    : $"There are **{statistics.DocumentsByType.Count} file types** in the system:\n\n");
                
                foreach (var type in statistics.DocumentsByType.OrderByDescending(x => x.Value))
                {
                    var typeName = GetFriendlyTypeName(type.Key);
                    answer.Append($"- **{typeName}**: {type.Value} ");
                    answer.Append(isItalian ? "documenti\n" : "documents\n");
                }
            }
            else
            {
                answer.Append(isItalian 
                    ? "Non ci sono documenti nel sistema." 
                    : "There are no documents in the system.");
            }
        }
        // Storage query
        else if (ContainsAny(normalizedQuery, "storage", "space", "size", "spazio", "dimensione"))
        {
            var storageMB = statistics.TotalStorageBytes / (1024.0 * 1024.0);
            var storageGB = storageMB / 1024.0;
            
            if (storageGB >= 1)
            {
                answer.Append(isItalian 
                    ? $"Lo spazio totale occupato è **{storageGB:F2} GB**." 
                    : $"Total storage used is **{storageGB:F2} GB**.");
            }
            else
            {
                answer.Append(isItalian 
                    ? $"Lo spazio totale occupato è **{storageMB:F2} MB**." 
                    : $"Total storage used is **{storageMB:F2} MB**.");
            }
        }
        // Recent uploads query
        else if (ContainsAny(normalizedQuery, "recent", "today", "this week", "this month", "recenti", "oggi", "questa settimana", "questo mese"))
        {
            answer.Append(isItalian ? "**Documenti caricati di recente:**\n\n" : "**Recently uploaded documents:**\n\n");
            answer.Append(isItalian 
                ? $"- Oggi: {statistics.DocumentsUploadedToday}\n"
                : $"- Today: {statistics.DocumentsUploadedToday}\n");
            answer.Append(isItalian 
                ? $"- Questa settimana: {statistics.DocumentsUploadedThisWeek}\n"
                : $"- This week: {statistics.DocumentsUploadedThisWeek}\n");
            answer.Append(isItalian 
                ? $"- Questo mese: {statistics.DocumentsUploadedThisMonth}"
                : $"- This month: {statistics.DocumentsUploadedThisMonth}");
        }
        // Default: provide overview
        else
        {
            answer.Append(isItalian 
                ? $"**Panoramica del sistema:**\n\n" 
                : $"**System Overview:**\n\n");
            answer.Append(isItalian 
                ? $"- Documenti totali: **{statistics.TotalDocuments}**\n" 
                : $"- Total documents: **{statistics.TotalDocuments}**\n");
            
            var storageMB = statistics.TotalStorageBytes / (1024.0 * 1024.0);
            answer.Append(isItalian 
                ? $"- Spazio utilizzato: **{storageMB:F2} MB**\n" 
                : $"- Storage used: **{storageMB:F2} MB**\n");
            
            if (statistics.DocumentsByCategory.Any())
            {
                answer.Append(isItalian 
                    ? $"- Categorie: **{statistics.DocumentsByCategory.Count}**\n" 
                    : $"- Categories: **{statistics.DocumentsByCategory.Count}**\n");
            }
            
            if (statistics.DocumentsByType.Any())
            {
                answer.Append(isItalian 
                    ? $"- Tipi di file: **{statistics.DocumentsByType.Count}**" 
                    : $"- File types: **{statistics.DocumentsByType.Count}**");
            }
        }

        return Task.FromResult(answer.ToString());
    }

    private bool ContainsItalianKeywords(string query)
    {
        var italianKeywords = new[] { "quanti", "quante", "nel sistema", "documenti", "categorie", "tipi" };
        return italianKeywords.Any(kw => query.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    private bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    private string GetFriendlyTypeName(string contentType)
    {
        return contentType switch
        {
            "application/pdf" => "PDF",
            "application/msword" => "Word (DOC)",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "Word (DOCX)",
            "application/vnd.ms-excel" => "Excel (XLS)",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "Excel (XLSX)",
            "application/vnd.ms-powerpoint" => "PowerPoint (PPT)",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => "PowerPoint (PPTX)",
            "text/plain" => "Text (TXT)",
            "image/jpeg" => "JPEG Image",
            "image/png" => "PNG Image",
            "image/gif" => "GIF Image",
            _ => contentType
        };
    }
}
