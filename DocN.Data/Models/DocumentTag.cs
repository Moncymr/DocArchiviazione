namespace DocN.Data.Models;

public class DocumentTag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    public int DocumentId { get; set; }
    public virtual Document Document { get; set; } = null!;
}
