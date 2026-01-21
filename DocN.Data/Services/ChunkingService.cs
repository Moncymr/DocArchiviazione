using DocN.Data.Models;
using System.Text;

namespace DocN.Data.Services;

/// <summary>
/// Service for chunking documents into smaller pieces for better vector search
/// </summary>
public interface IChunkingService
{
    /// <summary>
    /// Chunk a document's text into smaller pieces with overlap
    /// </summary>
    /// <param name="text">The text to chunk</param>
    /// <param name="chunkSize">Maximum characters per chunk</param>
    /// <param name="overlap">Number of overlapping characters between chunks</param>
    /// <returns>List of text chunks</returns>
    List<string> ChunkText(string text, int chunkSize = 1000, int overlap = 200);

    /// <summary>
    /// Create DocumentChunk entities from a document
    /// </summary>
    /// <param name="document">The document to chunk</param>
    /// <param name="chunkSize">Maximum characters per chunk</param>
    /// <param name="overlap">Number of overlapping characters between chunks</param>
    /// <returns>List of DocumentChunk entities (without embeddings)</returns>
    List<DocumentChunk> ChunkDocument(Document document, int chunkSize = 1000, int overlap = 200);

    /// <summary>
    /// Estimate token count for a text (rough estimation)
    /// </summary>
    /// <param name="text">Text to estimate</param>
    /// <returns>Estimated token count</returns>
    int EstimateTokenCount(string text);
}

/// <summary>
/// Servizio per suddividere documenti in chunk (porzioni di testo) ottimizzati per RAG e ricerca vettoriale
/// Implementa strategie di chunking intelligenti con overlap per preservare contesto
/// </summary>
/// <remarks>
/// Scopo: Suddividere documenti lunghi in porzioni gestibili per embedding generation e retrieval
/// 
/// Perché il chunking è necessario:
/// 1. Limiti dimensionali: Modelli embedding hanno limite input (es. 8192 token per OpenAI)
/// 2. Granularità ricerca: Chunk piccoli = ricerca più precisa e rilevante
/// 3. Performance: Embedding e retrieval più veloci su porzioni piccole
/// 4. Qualità risposte: Contesto focalizzato migliora risposte AI
/// 
/// Strategie implementate:
/// - Sliding window con overlap: Preserva contesto tra chunk
/// - Sentence-aware: Tenta di spezzare a fine frase (., !, ?)
/// - Word-boundary: Fallback a spazio per evitare parole tagliate
/// 
/// Best practices chunking:
/// - Dimensione chunk: 500-1500 caratteri (bilanciamento contesto/precisione)
/// - Overlap: 10-20% dimensione chunk (tipicamente 100-300 caratteri)
/// - Più è grande il documento, più chunk servono
/// </remarks>
public class ChunkingService : IChunkingService
{
    /// <summary>
    /// Suddivide testo in chunk utilizzando strategia sliding window con overlap intelligente
    /// </summary>
    /// <param name="text">Testo da suddividere</param>
    /// <param name="chunkSize">Dimensione massima caratteri per chunk (default: 1000)</param>
    /// <param name="overlap">Numero caratteri sovrapposti tra chunk consecutivi (default: 200)</param>
    /// <returns>Lista di stringhe (chunk di testo)</returns>
    /// <remarks>
    /// Scopo: Creare porzioni di testo ottimali per embedding generation e ricerca semantica
    /// 
    /// Algoritmo:
    /// 1. Inizia dalla posizione 0
    /// 2. Calcola fine chunk (position + chunkSize)
    /// 3. Cerca boundary intelligente:
    ///    a. Preferenza: Fine frase (., !, ?) negli ultimi 100 caratteri
    ///    b. Fallback: Spazio (word boundary) negli ultimi 100 caratteri
    ///    c. Ultima risorsa: Hard cut a chunkSize
    /// 4. Estrae chunk e aggiunge a lista
    /// 5. Avanza posizione di (chunkSize - overlap) per overlap
    /// 6. Ripete fino a fine testo
    /// 
    /// Vantaggi overlap:
    /// - Preserva contesto tra chunk (informazioni non perse a confine)
    /// - Migliora retrieval accuracy (query può matchare meglio)
    /// - Riduce "edge effects" del chunking
    /// 
    /// Output atteso:
    /// - Lista chunk, ciascuno <= chunkSize caratteri
    /// - Chunk consecutivi si sovrappongono per overlap caratteri
    /// - Chunk terminano preferibilmente a fine frase o parola
    /// - Lista vuota se testo è vuoto/null
    /// 
    /// Esempio:
    /// Text: "Questo è il primo paragrafo. Questo è il secondo paragrafo."
    /// ChunkSize: 30, Overlap: 10
    /// Chunk 1: "Questo è il primo paragrafo."
    /// Chunk 2: "o paragrafo. Questo è il secondo"
    /// Chunk 3: "secondo paragrafo."
    /// </remarks>
    public List<string> ChunkText(string text, int chunkSize = 1000, int overlap = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        if (overlap >= chunkSize)
            throw new ArgumentException("Overlap must be less than chunk size");

        var chunks = new List<string>();
        var position = 0;

        while (position < text.Length)
        {
            // Calculate chunk end position
            var endPosition = Math.Min(position + chunkSize, text.Length);
            
            // If not at the end, try to break at a sentence boundary
            if (endPosition < text.Length)
            {
                // Look for sentence boundaries (. ! ?) within the last 100 characters
                var searchStart = Math.Max(position, endPosition - 100);
                var lastPeriod = text.LastIndexOf('.', endPosition - 1, endPosition - searchStart);
                var lastExclamation = text.LastIndexOf('!', endPosition - 1, endPosition - searchStart);
                var lastQuestion = text.LastIndexOf('?', endPosition - 1, endPosition - searchStart);
                
                var boundary = Math.Max(lastPeriod, Math.Max(lastExclamation, lastQuestion));
                
                if (boundary > searchStart)
                {
                    endPosition = boundary + 1; // Include the punctuation
                }
                // If no sentence boundary, try to break at a word boundary
                else
                {
                    var lastSpace = text.LastIndexOf(' ', endPosition - 1, Math.Min(100, endPosition - position));
                    if (lastSpace > position)
                    {
                        endPosition = lastSpace;
                    }
                }
            }

            // Extract the chunk
            var chunkText = text.Substring(position, endPosition - position).Trim();
            
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                chunks.Add(chunkText);
            }

            // If we've reached the end of the text, we're done
            if (endPosition >= text.Length)
                break;

            // Move position forward, accounting for overlap
            var newPosition = endPosition - overlap;
            
            // Ensure we make forward progress - must move at least 1 character forward
            if (newPosition <= position)
            {
                // If overlap is too large or we're not making progress, skip overlap and move to endPosition
                newPosition = endPosition;
            }
            
            position = newPosition;
        }

        return chunks;
    }

    /// <summary>
    /// Create DocumentChunk entities from a document
    /// </summary>
    public List<DocumentChunk> ChunkDocument(Document document, int chunkSize = 1000, int overlap = 200)
    {
        var textChunks = ChunkText(document.ExtractedText, chunkSize, overlap);
        var documentChunks = new List<DocumentChunk>();
        
        var currentPosition = 0;
        for (int i = 0; i < textChunks.Count; i++)
        {
            var chunk = textChunks[i];
            var endPosition = currentPosition + chunk.Length;
            
            documentChunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = i,
                ChunkText = chunk,
                TokenCount = EstimateTokenCount(chunk),
                StartPosition = currentPosition,
                EndPosition = endPosition,
                CreatedAt = DateTime.UtcNow
            });
            
            // Update position for next chunk (accounting for overlap)
            currentPosition = endPosition - overlap;
        }

        return documentChunks;
    }

    /// <summary>
    /// Rough estimation of token count (1 token ≈ 4 characters for English text)
    /// This is a simple heuristic; for precise counts, use a tokenizer
    /// </summary>
    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // Simple estimation: 1 token ≈ 4 characters
        // This is a rough approximation that works reasonably well for English
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
