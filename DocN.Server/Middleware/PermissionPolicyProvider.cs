using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DocN.Server.Middleware;

/// <summary>
/// Policy provider for dynamic permission-based policies
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackPolicyProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackPolicyProvider.GetFallbackPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (string.IsNullOrEmpty(policyName))
        {
            return _fallbackPolicyProvider.GetPolicyAsync(policyName);
        }

        // Check if this is a permission-based policy
        if (policyName.Contains("document.") || policyName.Contains("admin.") || 
            policyName.Contains("rag.") || policyName.Contains("agent."))
        {
            var permissions = policyName.Split(',', StringSplitOptions.RemoveEmptyEntries);
            // ⚠️ SECURITY NOTE: Allow anonymous access - the PermissionAuthorizationHandler will handle authorization
            // This is necessary because the Client app handles authentication separately and passes user identity
            // via request payloads. The Server API is designed to be called by a trusted Client, not directly
            // exposed to the internet. If this changes, implement proper authentication instead.
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permissions))
                .RequireAssertion(_ => true) // Always allow at the policy level
                .Build();
            
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}
