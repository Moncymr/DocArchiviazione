namespace DocN.Data.Models;

/// <summary>
/// Rappresenta una conversazione chat tra utente e sistema RAG
/// Una conversazione può contenere più messaggi e mantiene il contesto
/// </summary>
public class Conversation
{
    /// <summary>
    /// Identificatore univoco della conversazione
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID dell'utente proprietario della conversazione
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Utente proprietario (relazione)
    /// </summary>
    public virtual ApplicationUser? User { get; set; }

    /// <summary>
    /// Titolo della conversazione (generato automaticamente dal primo messaggio)
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Data e ora di creazione della conversazione
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data e ora dell'ultimo messaggio
    /// Usato per ordinare le conversazioni
    /// </summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indica se la conversazione è stata archiviata
    /// </summary>
    public bool IsArchived { get; set; } = false;

    /// <summary>
    /// Indica se la conversazione è stata segnata come importante
    /// </summary>
    public bool IsStarred { get; set; } = false;

    /// <summary>
    /// Lista di messaggi nella conversazione
    /// </summary>
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    /// <summary>
    /// Tag associati alla conversazione (per organizzazione)
    /// </summary>
    public string? Tags { get; set; }
}

/// <summary>
/// Rappresenta un singolo messaggio in una conversazione
/// </summary>
public class Message
{
    /// <summary>
    /// Identificatore univoco del messaggio
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID della conversazione a cui appartiene
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// Conversazione di appartenenza (relazione)
    /// </summary>
    public virtual Conversation? Conversation { get; set; }

    /// <summary>
    /// Ruolo del messaggio: "user" (utente) o "assistant" (AI)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Contenuto del messaggio
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Lista di IDs dei documenti referenziati in questo messaggio
    /// Memorizzato come JSON array: [1, 5, 12]
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<int> ReferencedDocumentIds
    {
        get
        {
            if (string.IsNullOrEmpty(ReferencedDocumentIdsJson))
                return new List<int>();
            
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<int>>(ReferencedDocumentIdsJson) ?? new List<int>();
            }
            catch (System.Text.Json.JsonException)
            {
                // Return empty list if JSON is malformed
                return new List<int>();
            }
        }
        set => ReferencedDocumentIdsJson = value == null || value.Count == 0 
            ? null 
            : System.Text.Json.JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// Backing field per ReferencedDocumentIds - memorizza il JSON come stringa
    /// </summary>
    public string? ReferencedDocumentIdsJson { get; set; }

    /// <summary>
    /// Data e ora del messaggio
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indica se il messaggio contiene un errore
    /// </summary>
    public bool IsError { get; set; } = false;

    /// <summary>
    /// Metadati aggiuntivi (JSON)
    /// Può contenere: token count, modello usato, latenza, ecc.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Rating del messaggio da parte dell'utente (1-5, null = non valutato)
    /// Usato per migliorare il sistema
    /// </summary>
    public int? UserRating { get; set; }

    /// <summary>
    /// Feedback testuale dell'utente su questo messaggio
    /// </summary>
    public string? UserFeedback { get; set; }
}
