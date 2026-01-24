namespace DocN.Data.Constants;

/// <summary>
/// Defines the role hierarchy for the application
/// </summary>
public static class Roles
{
    /// <summary>
    /// Super Administrator - Full system access across all tenants
    /// </summary>
    public const string SuperAdmin = "SuperAdmin";
    
    /// <summary>
    /// Tenant Administrator - Full access within their tenant
    /// </summary>
    public const string TenantAdmin = "TenantAdmin";
    
    /// <summary>
    /// Power User - Advanced features and document management
    /// </summary>
    public const string PowerUser = "PowerUser";
    
    /// <summary>
    /// Standard User - Basic document access and operations
    /// </summary>
    public const string User = "User";
    
    /// <summary>
    /// Read Only - View documents only, no modifications
    /// </summary>
    public const string ReadOnly = "ReadOnly";
    
    /// <summary>
    /// All available roles
    /// </summary>
    public static readonly string[] All = new[]
    {
        SuperAdmin,
        TenantAdmin,
        PowerUser,
        User,
        ReadOnly
    };
    
    /// <summary>
    /// Roles that can manage documents
    /// </summary>
    public static readonly string[] DocumentManagers = new[]
    {
        SuperAdmin,
        TenantAdmin,
        PowerUser,
        User
    };
    
    /// <summary>
    /// Roles with administrative privileges
    /// </summary>
    public static readonly string[] Administrators = new[]
    {
        SuperAdmin,
        TenantAdmin
    };
}
