using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DocN.Core.Interfaces;
using DocN.Data.Models;
using System.Text;
using System.Text.Json;
using System.Globalization;

#pragma warning disable SKEXP0010 // ResponseFormat is experimental

namespace DocN.Data.Services;

/// <summary>
/// Implementazione del servizio Self-Query per estrazione filtri da linguaggio naturale
/// </summary>
public class SelfQueryService : ISelfQueryService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<SelfQueryService> _logger;
    private readonly ISemanticRAGService _ragService;
    private readonly ApplicationDbContext _context;

    public SelfQueryService(
        Kernel kernel,
        ILogger<SelfQueryService> logger,
        ISemanticRAGService ragService,
        ApplicationDbContext context)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
        _ragService = ragService;
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<SelfQueryResult> ParseQueryWithFiltersAsync(
        string naturalLanguageQuery,
        List<FilterDefinition>? availableFilters = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Parsing self-query: {Query}", naturalLanguageQuery);

            // Ottieni filtri disponibili se non forniti
            availableFilters ??= await GetAvailableFiltersAsync();

            // Costruisci prompt per LLM
            var prompt = BuildFilterExtractionPrompt(naturalLanguageQuery, availableFilters);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(@"Sei un esperto di query parsing. 
Estrai la query semantica e i filtri strutturati da query in linguaggio naturale.
Restituisci sempre JSON valido.");
            chatHistory.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 500,
                Temperature = 0.1, // Bassa temperatura per maggiore precisione
                TopP = 0.9,
                ResponseFormat = "json_object"
            };

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            var jsonResponse = result.Content?.Trim() ?? "{}";

            // Parse JSON response
            var parsedResult = ParseFilterExtractionResponse(jsonResponse, naturalLanguageQuery);
            
            stopwatch.Stop();
            _logger.LogInformation("Parsed self-query in {ElapsedMs}ms. Extracted {FilterCount} filters",
                stopwatch.ElapsedMilliseconds, parsedResult.Filters.Count);

            return parsedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing self-query: {Query}", naturalLanguageQuery);
            stopwatch.Stop();
            
            return new SelfQueryResult
            {
                OriginalQuery = naturalLanguageQuery,
                SemanticQuery = naturalLanguageQuery,
                Success = false,
                Messages = new List<string> { $"Errore durante il parsing: {ex.Message}" }
            };
        }
    }

    /// <inheritdoc/>
    public async Task<List<RelevantDocumentResult>> SearchWithFiltersAsync(
        string semanticQuery,
        List<ExtractedFilter> filters,
        string userId,
        int topK = 10)
    {
        try
        {
            _logger.LogDebug("Searching with {FilterCount} filters for query: {Query}",
                filters.Count, semanticQuery);

            // Costruisci query LINQ con filtri
            var query = _context.Documents.AsQueryable();

            // Applica filtro owner
            query = query.Where(d => d.OwnerId == userId);

            // Applica filtri estratti
            foreach (var filter in filters)
            {
                query = ApplyFilter(query, filter);
            }

            // Ottieni documenti filtrati
            var filteredDocs = await query.ToListAsync();

            _logger.LogDebug("Filtered to {Count} documents", filteredDocs.Count);

            if (filteredDocs.Count == 0)
            {
                return new List<RelevantDocumentResult>();
            }

            // Estrai IDs dei documenti filtrati
            var docIds = filteredDocs.Select(d => d.Id).ToList();

            // Esegui ricerca semantica solo sui documenti filtrati
            var results = await _ragService.SearchDocumentsAsync(semanticQuery, userId, topK, 0.5);

            // Filtra risultati per includere solo i documenti che passano i filtri
            var filteredResults = results.Where(r => docIds.Contains(r.DocumentId)).ToList();

            _logger.LogInformation("Search with filters completed. Returned {Count} results", filteredResults.Count);

            return filteredResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching with filters");
            // Fallback a ricerca senza filtri
            return await _ragService.SearchDocumentsAsync(semanticQuery, userId, topK, 0.7);
        }
    }

    /// <inheritdoc/>
    public async Task<SelfQuerySearchResult> ExecuteSelfQueryAsync(
        string naturalLanguageQuery,
        string userId,
        int topK = 10)
    {
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var filterStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Executing self-query: {Query}", naturalLanguageQuery);

            // Fase 1: Parse query ed estrai filtri
            var parseResult = await ParseQueryWithFiltersAsync(naturalLanguageQuery);
            filterStopwatch.Stop();

            if (!parseResult.Success)
            {
                _logger.LogWarning("Filter extraction failed, using original query");
                var fallbackResults = await _ragService.SearchDocumentsAsync(
                    naturalLanguageQuery, userId, topK, 0.7);
                
                return new SelfQuerySearchResult
                {
                    Results = fallbackResults,
                    SemanticQuery = naturalLanguageQuery,
                    AppliedFilters = new List<ExtractedFilter>(),
                    Statistics = new SearchStatistics
                    {
                        ReturnedResults = fallbackResults.Count,
                        FilterExtractionTimeMs = filterStopwatch.ElapsedMilliseconds,
                        SearchTimeMs = totalStopwatch.ElapsedMilliseconds
                    }
                };
            }

            // Fase 2: Valida e normalizza filtri
            var availableFilters = await GetAvailableFiltersAsync();
            var validatedFilters = await ValidateAndNormalizeFiltersAsync(
                parseResult.Filters, availableFilters);

            // Fase 3: Esegui ricerca con filtri
            var searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var totalDocs = await _context.Documents.CountAsync(d => d.OwnerId == userId);
            
            var results = await SearchWithFiltersAsync(
                parseResult.SemanticQuery,
                validatedFilters,
                userId,
                topK);
            
            searchStopwatch.Stop();
            totalStopwatch.Stop();

            // Calcola statistiche
            var filteredCount = results.Count > 0 ? 
                await _context.Documents.CountAsync(d => d.OwnerId == userId) : 0;

            var searchResult = new SelfQuerySearchResult
            {
                Results = results,
                SemanticQuery = parseResult.SemanticQuery,
                AppliedFilters = validatedFilters,
                Statistics = new SearchStatistics
                {
                    TotalDocuments = totalDocs,
                    FilteredDocuments = filteredCount,
                    ReturnedResults = results.Count,
                    FilterExtractionTimeMs = filterStopwatch.ElapsedMilliseconds,
                    SearchTimeMs = searchStopwatch.ElapsedMilliseconds,
                    FiltersExtracted = validatedFilters.Count
                }
            };

            _logger.LogInformation(
                "Self-query completed in {TotalMs}ms. Extracted {FilterCount} filters, returned {ResultCount} results",
                totalStopwatch.ElapsedMilliseconds, validatedFilters.Count, results.Count);

            return searchResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing self-query");
            totalStopwatch.Stop();

            // Fallback a ricerca standard
            var fallbackResults = await _ragService.SearchDocumentsAsync(
                naturalLanguageQuery, userId, topK, 0.7);

            return new SelfQuerySearchResult
            {
                Results = fallbackResults,
                SemanticQuery = naturalLanguageQuery,
                AppliedFilters = new List<ExtractedFilter>(),
                Statistics = new SearchStatistics
                {
                    ReturnedResults = fallbackResults.Count,
                    SearchTimeMs = totalStopwatch.ElapsedMilliseconds
                }
            };
        }
    }

    /// <inheritdoc/>
    public async Task<List<ExtractedFilter>> ValidateAndNormalizeFiltersAsync(
        List<ExtractedFilter> filters,
        List<FilterDefinition> availableFilters)
    {
        var validatedFilters = new List<ExtractedFilter>();

        foreach (var filter in filters)
        {
            try
            {
                // Trova definizione del filtro
                var definition = availableFilters.FirstOrDefault(
                    f => f.Field.Equals(filter.Field, StringComparison.OrdinalIgnoreCase));

                if (definition == null)
                {
                    _logger.LogWarning("Unknown filter field: {Field}", filter.Field);
                    continue;
                }

                // Verifica operatore supportato
                if (!definition.SupportedOperators.Contains(filter.Operator))
                {
                    _logger.LogWarning("Unsupported operator {Operator} for field {Field}",
                        filter.Operator, filter.Field);
                    continue;
                }

                // Normalizza valore basato sul tipo
                var normalizedValue = NormalizeFilterValue(filter.Value, definition.DataType);
                if (normalizedValue == null)
                {
                    _logger.LogWarning("Invalid value {Value} for field {Field}",
                        filter.Value, filter.Field);
                    continue;
                }

                filter.Value = normalizedValue;
                filter.ValueType = definition.DataType;
                validatedFilters.Add(filter);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating filter: {Field}", filter.Field);
            }
        }

        return await Task.FromResult(validatedFilters);
    }

    /// <inheritdoc/>
    public async Task<List<FilterDefinition>> GetAvailableFiltersAsync()
    {
        // Definisci filtri disponibili nel sistema
        var filters = new List<FilterDefinition>
        {
            new FilterDefinition
            {
                Field = "Category",
                DisplayName = "Categoria",
                DataType = FilterValueType.String,
                SupportedOperators = new List<FilterOperator> 
                { 
                    FilterOperator.Equals, 
                    FilterOperator.NotEquals,
                    FilterOperator.In 
                },
                Description = "Categoria del documento",
                Examples = new List<string> 
                { 
                    "categoria fatture", 
                    "solo documenti HR",
                    "contratti o fatture"
                }
            },
            new FilterDefinition
            {
                Field = "UploadDate",
                DisplayName = "Data di Upload",
                DataType = FilterValueType.Date,
                SupportedOperators = new List<FilterOperator>
                {
                    FilterOperator.Equals,
                    FilterOperator.GreaterThan,
                    FilterOperator.GreaterThanOrEqual,
                    FilterOperator.LessThan,
                    FilterOperator.LessThanOrEqual
                },
                Description = "Data di caricamento del documento",
                Examples = new List<string>
                {
                    "ultimi 3 mesi",
                    "da gennaio 2024",
                    "prima del 2023"
                }
            },
            new FilterDefinition
            {
                Field = "FileName",
                DisplayName = "Nome File",
                DataType = FilterValueType.String,
                SupportedOperators = new List<FilterOperator>
                {
                    FilterOperator.Contains,
                    FilterOperator.StartsWith,
                    FilterOperator.EndsWith,
                    FilterOperator.Equals
                },
                Description = "Nome del file del documento",
                Examples = new List<string>
                {
                    "file che iniziano con 'report'",
                    "file PDF",
                    "contiene 'budget'"
                }
            }
        };

        return await Task.FromResult(filters);
    }

    /// <summary>
    /// Costruisce il prompt per l'estrazione dei filtri
    /// </summary>
    private string BuildFilterExtractionPrompt(string query, List<FilterDefinition> availableFilters)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Analizza la seguente query in linguaggio naturale ed estrai:");
        promptBuilder.AppendLine("1. La query semantica (senza filtri)");
        promptBuilder.AppendLine("2. I filtri strutturati");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"Query: {query}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Filtri disponibili:");
        
        foreach (var filter in availableFilters)
        {
            promptBuilder.AppendLine($"- {filter.Field} ({filter.DataType}): {filter.Description}");
            promptBuilder.AppendLine($"  Esempi: {string.Join(", ", filter.Examples)}");
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Restituisci in formato JSON:");
        promptBuilder.AppendLine(@"{
  ""semanticQuery"": ""query senza filtri"",
  ""filters"": [
    {
      ""field"": ""nome campo"",
      ""operator"": ""Equals|GreaterThan|Contains|etc"",
      ""value"": ""valore"",
      ""logicalOperator"": ""And|Or""
    }
  ]
}");

        return promptBuilder.ToString();
    }

    /// <summary>
    /// Parse la risposta JSON dell'estrazione filtri
    /// </summary>
    private SelfQueryResult ParseFilterExtractionResponse(string jsonResponse, string originalQuery)
    {
        var result = new SelfQueryResult
        {
            OriginalQuery = originalQuery,
            Success = false
        };

        try
        {
            var jsonDoc = JsonDocument.Parse(jsonResponse);
            var root = jsonDoc.RootElement;

            // Estrai semantic query
            if (root.TryGetProperty("semanticQuery", out var semQuery))
            {
                result.SemanticQuery = semQuery.GetString() ?? originalQuery;
            }
            else
            {
                result.SemanticQuery = originalQuery;
            }

            // Estrai filtri
            if (root.TryGetProperty("filters", out var filtersArray))
            {
                foreach (var filterElement in filtersArray.EnumerateArray())
                {
                    var filter = ParseFilterElement(filterElement);
                    if (filter != null)
                    {
                        result.Filters.Add(filter);
                    }
                }
            }

            result.Success = true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse filter extraction JSON");
            result.SemanticQuery = originalQuery;
            result.Messages.Add("Errore nel parsing JSON");
        }

        return result;
    }

    /// <summary>
    /// Parse un singolo elemento filtro dal JSON
    /// </summary>
    private ExtractedFilter? ParseFilterElement(JsonElement filterElement)
    {
        try
        {
            var filter = new ExtractedFilter();

            if (filterElement.TryGetProperty("field", out var fieldProp))
                filter.Field = fieldProp.GetString() ?? "";

            if (filterElement.TryGetProperty("operator", out var opProp))
            {
                var opStr = opProp.GetString();
                if (Enum.TryParse<FilterOperator>(opStr, true, out var op))
                    filter.Operator = op;
            }

            if (filterElement.TryGetProperty("value", out var valueProp))
            {
                filter.Value = valueProp.ValueKind switch
                {
                    JsonValueKind.String => valueProp.GetString() ?? "",
                    JsonValueKind.Number => valueProp.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => valueProp.GetString() ?? ""
                };
            }

            if (filterElement.TryGetProperty("logicalOperator", out var logicalProp))
            {
                var logStr = logicalProp.GetString();
                if (Enum.TryParse<LogicalOperator>(logStr, true, out var logOp))
                    filter.LogicalOperator = logOp;
            }

            return string.IsNullOrEmpty(filter.Field) ? null : filter;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing filter element");
            return null;
        }
    }

    /// <summary>
    /// Normalizza il valore del filtro in base al tipo
    /// </summary>
    private object? NormalizeFilterValue(object value, FilterValueType dataType)
    {
        try
        {
            return dataType switch
            {
                FilterValueType.String => value?.ToString(),
                FilterValueType.Number => Convert.ToDouble(value),
                FilterValueType.Boolean => Convert.ToBoolean(value),
                FilterValueType.Date => ParseDate(value?.ToString()),
                _ => value
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse date da vari formati
    /// </summary>
    private DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // Prova parsing standard
        if (DateTime.TryParse(dateStr, out var date))
            return date;

        // Gestisci espressioni relative (es: "ultimi 3 mesi")
        var now = DateTime.UtcNow;
        if (dateStr.Contains("ultimi") || dateStr.Contains("last"))
        {
            if (dateStr.Contains("mesi") || dateStr.Contains("month"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(dateStr, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int months))
                {
                    return now.AddMonths(-months);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Applica un filtro alla query LINQ
    /// </summary>
    private IQueryable<Document> ApplyFilter(IQueryable<Document> query, ExtractedFilter filter)
    {
        try
        {
            switch (filter.Field.ToLower())
            {
                case "category":
                    var categoryValue = filter.Value?.ToString();
                    return filter.Operator switch
                    {
                        FilterOperator.Equals => query.Where(d => d.ActualCategory == categoryValue),
                        FilterOperator.NotEquals => query.Where(d => d.ActualCategory != categoryValue),
                        _ => query
                    };

                case "uploaddate":
                    if (filter.Value is DateTime dateValue)
                    {
                        return filter.Operator switch
                        {
                            FilterOperator.GreaterThan => query.Where(d => d.UploadedAt > dateValue),
                            FilterOperator.GreaterThanOrEqual => query.Where(d => d.UploadedAt >= dateValue),
                            FilterOperator.LessThan => query.Where(d => d.UploadedAt < dateValue),
                            FilterOperator.LessThanOrEqual => query.Where(d => d.UploadedAt <= dateValue),
                            _ => query
                        };
                    }
                    return query;

                case "filename":
                    var filenameValue = filter.Value?.ToString() ?? "";
                    return filter.Operator switch
                    {
                        FilterOperator.Contains => query.Where(d => d.FileName.Contains(filenameValue)),
                        FilterOperator.StartsWith => query.Where(d => d.FileName.StartsWith(filenameValue)),
                        FilterOperator.EndsWith => query.Where(d => d.FileName.EndsWith(filenameValue)),
                        FilterOperator.Equals => query.Where(d => d.FileName == filenameValue),
                        _ => query
                    };
            }

            return query;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying filter {Field}", filter.Field);
            return query;
        }
    }
}
