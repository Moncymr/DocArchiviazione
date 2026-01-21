namespace DocN.Data.Models;

public class DocumentShare
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public virtual Document Document { get; set; } = null!;
    
    public string SharedWithUserId { get; set; } = string.Empty;
    public virtual ApplicationUser SharedWithUser { get; set; } = null!;
    
    public DocumentPermission Permission { get; set; } = DocumentPermission.Read;
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    public string? SharedByUserId { get; set; }
}

public enum DocumentPermission
{
    Read = 0,
    Write = 1,
    Delete = 2
}
