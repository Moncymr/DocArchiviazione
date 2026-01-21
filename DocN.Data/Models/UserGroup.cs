namespace DocN.Data.Models;

/// <summary>
/// Represents a group of users for document sharing and access control
/// </summary>
public class UserGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Owner of the group
    public string? OwnerId { get; set; }
    public virtual ApplicationUser? Owner { get; set; }
    
    // Multi-tenant support
    public int? TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
    
    // Navigation properties
    public virtual ICollection<UserGroupMember> Members { get; set; } = new List<UserGroupMember>();
    public virtual ICollection<DocumentGroupShare> DocumentShares { get; set; } = new List<DocumentGroupShare>();
}

/// <summary>
/// Represents a user's membership in a group
/// </summary>
public class UserGroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public virtual UserGroup Group { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;
    
    public UserGroupRole Role { get; set; } = UserGroupRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents sharing a document with a group
/// </summary>
public class DocumentGroupShare
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public virtual Document Document { get; set; } = null!;
    
    public int GroupId { get; set; }
    public virtual UserGroup Group { get; set; } = null!;
    
    public DocumentPermission Permission { get; set; } = DocumentPermission.Read;
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    public string? SharedByUserId { get; set; }
}

/// <summary>
/// Role of a user within a group
/// </summary>
public enum UserGroupRole
{
    Member = 0,  // Regular member
    Admin = 1    // Can manage group members
}
