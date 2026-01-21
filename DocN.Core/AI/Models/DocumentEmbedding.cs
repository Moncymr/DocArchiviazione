namespace DocN.Core.AI.Models;

/// <summary>
/// Rappresenta l'embedding vettoriale di un documento
/// </summary>
public class DocumentEmbedding
{
    /// <summary>
    /// ID del documento
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;
    
    /// <summary>
    /// Vettore di embedding
    /// </summary>
    public float[] Vector { get; set; } = Array.Empty<float>();
    
    /// <summary>
    /// Modello utilizzato per generare l'embedding
    /// </summary>
    public string Model { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp di creazione
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
