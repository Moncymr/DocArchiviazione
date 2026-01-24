namespace DocN.Data.Constants;

/// <summary>
/// Defines granular permissions for the application
/// </summary>
public static class Permissions
{
    // Document Permissions
    public const string DocumentRead = "document.read";
    public const string DocumentWrite = "document.write";
    public const string DocumentDelete = "document.delete";
    public const string DocumentShare = "document.share";
    public const string DocumentUpload = "document.upload";
    
    // Admin Permissions
    public const string AdminUsers = "admin.users";
    public const string AdminRoles = "admin.roles";
    public const string AdminTenants = "admin.tenants";
    public const string AdminSystem = "admin.system";
    public const string AdminAll = "admin.*";
    
    // RAG Configuration Permissions
    public const string RagConfig = "rag.config";
    public const string RagView = "rag.view";
    public const string RagExecute = "rag.execute";
    
    // Agent Permissions
    public const string AgentManage = "agent.manage";
    public const string AgentExecute = "agent.execute";
    
    /// <summary>
    /// Gets permissions for a specific role
    /// </summary>
    public static string[] GetPermissionsForRole(string role)
    {
        return role switch
        {
            Roles.SuperAdmin => new[]
            {
                DocumentRead, DocumentWrite, DocumentDelete, DocumentShare, DocumentUpload,
                AdminAll, AdminUsers, AdminRoles, AdminTenants, AdminSystem,
                RagConfig, RagView, RagExecute,
                AgentManage, AgentExecute
            },
            Roles.TenantAdmin => new[]
            {
                DocumentRead, DocumentWrite, DocumentDelete, DocumentShare, DocumentUpload,
                AdminUsers, AdminRoles,
                RagConfig, RagView, RagExecute,
                AgentManage, AgentExecute
            },
            Roles.PowerUser => new[]
            {
                DocumentRead, DocumentWrite, DocumentDelete, DocumentShare, DocumentUpload,
                RagView, RagExecute,
                AgentExecute
            },
            Roles.User => new[]
            {
                DocumentRead, DocumentWrite, DocumentUpload,
                RagView, RagExecute
            },
            Roles.ReadOnly => new[]
            {
                DocumentRead,
                RagView
            },
            _ => Array.Empty<string>()
        };
    }
}
