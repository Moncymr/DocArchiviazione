using DocN.Core.AI.Interfaces;
using DocN.Core.AI.Models;
using Microsoft.Extensions.Logging;

namespace DocN.Core.AI.Providers;

/// <summary>
/// Classe base astratta per i provider AI
/// </summary>
public abstract class BaseAIProvider : IDocumentAIProvider
{
    protected readonly ILogger _logger;

    protected BaseAIProvider(ILogger logger)
    {
        _logger = logger;
    }

    public abstract AIProviderType ProviderType { get; }
    public abstract string ProviderName { get; }

    public abstract Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    public abstract Task<List<CategorySuggestion>> SuggestCategoriesAsync(
        string documentText, 
        List<string> availableCategories, 
        CancellationToken cancellationToken = default);

    public abstract Task<List<string>> ExtractTagsAsync(
        string documentText,
        CancellationToken cancellationToken = default);

    public abstract Task<Dictionary<string, string>> ExtractMetadataAsync(
        string documentText,
        string fileName = "",
        CancellationToken cancellationToken = default);

    public virtual async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        string documentText, 
        List<string> availableCategories, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing document with {Provider}", ProviderName);
        
        var result = new DocumentAnalysisResult
        {
            ExtractedText = documentText
        };

        // Genera embedding - non bloccare se fallisce
        try
        {
            var embedding = await GenerateEmbeddingAsync(documentText, cancellationToken);
            result.Embedding = new DocumentEmbedding
            {
                Vector = embedding,
                Model = ProviderName,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding - continuing with analysis");
        }

        // Suggerisci categorie - sempre eseguire, anche se embedding fallisce
        try
        {
            result.CategorySuggestions = await SuggestCategoriesAsync(documentText, availableCategories, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting categories");
        }

        // Estrai tag - sempre eseguire, anche se embedding fallisce
        try
        {
            result.ExtractedTags = await ExtractTagsAsync(documentText, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting tags");
        }

        // Estrai metadati - sempre eseguire
        try
        {
            result.Metadata = await ExtractMetadataAsync(documentText, "", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata");
        }

        return result;
    }

    protected string BuildCategorySuggestionPrompt(string documentText, List<string> availableCategories)
    {
        var categoriesList = string.Join(", ", availableCategories);
        return $@"Analizza il seguente documento e suggerisci le categorie più appropriate tra quelle disponibili.

Categorie disponibili: {categoriesList}

Documento:
{documentText}

Fornisci un JSON con un array 'suggestions' contenente oggetti con:
- categoryName: nome della categoria
- confidence: valore da 0 a 1
- reasoning: breve motivazione

Rispondi SOLO con JSON valido, senza altri commenti.";
    }

    protected string BuildTagExtractionPrompt(string documentText)
    {
        var text = documentText.Length > 2000 
            ? documentText.Substring(0, 2000) 
            : documentText;
            
        return $@"Estrai 5-10 tag o parole chiave rilevanti dal seguente documento.
I tag devono essere brevi, specifici e rappresentativi del contenuto.

Documento:
{text}

Fornisci un JSON con un array 'tags' contenente le parole chiave estratte.
Esempio: {{""tags"": [""contratto"", ""legale"", ""2024"", ""servizi""]}}

Rispondi SOLO con JSON valido, senza altri commenti.";
    }

    protected string BuildMetadataExtractionPrompt(string documentText, string fileName)
    {
        var text = documentText.Length > 3000 
            ? documentText.Substring(0, 3000) 
            : documentText;
            
        return $@"Estrai metadati strutturati dal seguente documento. Analizza il contenuto e identifica informazioni chiave.

Nome file: {fileName}

Documento:
{text}

Cerca di estrarre il maggior numero di metadati rilevanti, ad esempio:
- Per FATTURE: invoice_number, invoice_date, invoice_year, total_amount, currency, vendor_name, customer_name, tax_id, payment_terms
- Per CONTRATTI: contract_number, contract_date, contract_year, parties, expiration_date, renewal_terms, contract_value
- Per DOCUMENTI GENERALI: document_type, author, creation_date, title, subject, company_name, reference_number, language
- Altri metadati rilevanti specifici del tipo di documento

Fornisci un JSON con coppie chiave-valore dei metadati estratti.
Usa nomi di campo in inglese e snake_case (es. invoice_number, creation_date).
Se un campo non è presente, NON includerlo nel risultato.

Esempio: {{""document_type"": ""invoice"", ""invoice_number"": ""INV-2024-001"", ""invoice_date"": ""2024-01-15"", ""total_amount"": ""1000.00"", ""currency"": ""EUR""}}

Rispondi SOLO con JSON valido, senza altri commenti.";
    }
}
