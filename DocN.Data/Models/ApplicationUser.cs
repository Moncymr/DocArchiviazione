using Microsoft.AspNetCore.Identity;

namespace DocN.Data.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Multi-tenant support
    public int? TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
    
    // Navigation properties
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<DocumentShare> SharedDocuments { get; set; } = new List<DocumentShare>();
    public virtual ICollection<UserGroup> OwnedGroups { get; set; } = new List<UserGroup>();
    public virtual ICollection<UserGroupMember> GroupMemberships { get; set; } = new List<UserGroupMember>();
}
