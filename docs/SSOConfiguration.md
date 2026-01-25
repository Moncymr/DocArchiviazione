# SSO Configuration Guide

## Overview

This guide explains how to configure Single Sign-On (SSO) for DocN using Azure AD, Okta, or SAML 2.0 providers.

## Prerequisites

- ASP.NET Core 8+ application
- Access to Azure AD tenant or Okta organization
- SSL/TLS certificate for production

## Option 1: Azure AD (Microsoft Entra ID)

### 1. Azure AD App Registration

1. Navigate to [Azure Portal](https://portal.azure.com)
2. Go to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Configure:
   - Name: `DocN Document Archiving`
   - Supported account types: `Accounts in this organizational directory only`
   - Redirect URI: `https://your-domain.com/signin-oidc`
5. Note the **Application (client) ID** and **Directory (tenant) ID**

### 2. Create Client Secret

1. In your app registration, go to **Certificates & secrets**
2. Click **New client secret**
3. Add description: `DocN Production`
4. Set expiration (12-24 months recommended)
5. **Copy the secret value immediately** (shown only once)

### 3. Configure API Permissions

1. Go to **API permissions**
2. Add permissions:
   - `Microsoft Graph` > `User.Read`
   - `Microsoft Graph` > `email`
   - `Microsoft Graph` > `openid`
   - `Microsoft Graph` > `profile`
3. Grant admin consent

### 4. Update appsettings.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-domain.com",
    "TenantId": "YOUR-TENANT-ID",
    "ClientId": "YOUR-CLIENT-ID",
    "ClientSecret": "YOUR-CLIENT-SECRET",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  }
}
```

### 5. Install NuGet Packages

```bash
dotnet add package Microsoft.Identity.Web
dotnet add package Microsoft.Identity.Web.UI
```

### 6. Update Program.cs

```csharp
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Add Azure AD authentication
builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "AzureAd")
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

// Add Razor Pages with Microsoft Identity UI
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.Run();
```

### 7. Map Azure AD Roles to DocN Roles

Create `AzureADRoleMappingService.cs`:

```csharp
public class AzureADRoleMappingService
{
    private readonly ILogger<AzureADRoleMappingService> _logger;

    public AzureADRoleMappingService(ILogger<AzureADRoleMappingService> logger)
    {
        _logger = logger;
    }

    public string MapAzureRoleToDocNRole(string azureRole)
    {
        return azureRole switch
        {
            "DocN.SuperAdmin" => Roles.SuperAdmin,
            "DocN.TenantAdmin" => Roles.TenantAdmin,
            "DocN.PowerUser" => Roles.PowerUser,
            "DocN.User" => Roles.User,
            _ => Roles.ReadOnly
        };
    }

    public async Task SyncUserRolesAsync(ClaimsPrincipal user, UserManager<ApplicationUser> userManager)
    {
        var email = user.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            return;

        var dbUser = await userManager.FindByEmailAsync(email);
        if (dbUser == null)
        {
            // Create user if doesn't exist
            dbUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(dbUser);
        }

        // Get Azure AD roles from claims
        var azureRoles = user.FindAll("roles").Select(c => c.Value).ToList();
        
        // Map and assign roles
        foreach (var azureRole in azureRoles)
        {
            var docNRole = MapAzureRoleToDocNRole(azureRole);
            if (!await userManager.IsInRoleAsync(dbUser, docNRole))
            {
                await userManager.AddToRoleAsync(dbUser, docNRole);
                _logger.LogInformation("Added user {Email} to role {Role}", email, docNRole);
            }
        }
    }
}
```

---

## Option 2: Okta

### 1. Okta Application Setup

1. Log in to [Okta Admin Console](https://YOUR-DOMAIN.okta.com/admin)
2. Go to **Applications** > **Create App Integration**
3. Select **OIDC - OpenID Connect**
4. Select **Web Application**
5. Configure:
   - App integration name: `DocN`
   - Sign-in redirect URIs: `https://your-domain.com/authorization-code/callback`
   - Sign-out redirect URIs: `https://your-domain.com/signout-callback`
   - Assignments: Select appropriate groups
6. Save and note **Client ID** and **Client Secret**

### 2. Update appsettings.json

```json
{
  "Okta": {
    "OktaDomain": "https://YOUR-DOMAIN.okta.com",
    "ClientId": "YOUR-CLIENT-ID",
    "ClientSecret": "YOUR-CLIENT-SECRET",
    "CallbackPath": "/authorization-code/callback",
    "SignedOutCallbackPath": "/signout-callback"
  }
}
```

### 3. Install NuGet Package

```bash
dotnet add package Okta.AspNetCore
```

### 4. Update Program.cs

```csharp
using Okta.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OktaDefaults.MvcAuthenticationScheme;
})
.AddCookie()
.AddOktaMvc(new OktaMvcOptions
{
    OktaDomain = builder.Configuration["Okta:OktaDomain"],
    ClientId = builder.Configuration["Okta:ClientId"],
    ClientSecret = builder.Configuration["Okta:ClientSecret"],
    CallbackPath = builder.Configuration["Okta:CallbackPath"],
    PostLogoutRedirectUri = builder.Configuration["Okta:SignedOutCallbackPath"]
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.Run();
```

---

## Option 3: Generic SAML 2.0

### 1. Install Package

```bash
dotnet add package Sustainsys.Saml2.AspNetCore2
```

### 2. Configure SAML Settings

```json
{
  "Saml2": {
    "EntityId": "https://your-domain.com/saml",
    "ReturnUrl": "https://your-domain.com/",
    "SigningCertificate": {
      "StoreName": "My",
      "StoreLocation": "CurrentUser",
      "Thumbprint": "YOUR-CERT-THUMBPRINT"
    },
    "IdentityProvider": {
      "EntityId": "https://idp.example.com/saml",
      "SingleSignOnServiceUrl": "https://idp.example.com/saml/sso",
      "SingleLogoutServiceUrl": "https://idp.example.com/saml/logout",
      "Certificate": "MIIDdTCCAl2gAwIBAgIL..."
    }
  }
}
```

### 3. Update Program.cs

```csharp
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Metadata;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Saml2Defaults.Scheme;
})
.AddCookie()
.AddSaml2(options =>
{
    options.SPOptions.EntityId = new EntityId(builder.Configuration["Saml2:EntityId"]);
    options.SPOptions.ReturnUrl = new Uri(builder.Configuration["Saml2:ReturnUrl"]);
    
    var idp = new IdentityProvider(
        new EntityId(builder.Configuration["Saml2:IdentityProvider:EntityId"]),
        options.SPOptions)
    {
        SingleSignOnServiceUrl = new Uri(builder.Configuration["Saml2:IdentityProvider:SingleSignOnServiceUrl"])
    };
    
    options.IdentityProviders.Add(idp);
});
```

---

## Session Management

### Configure Secure Sessions

```csharp
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Use distributed session if using multiple servers
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "DocNSession_";
});
```

---

## Testing SSO

### 1. Local Testing with Azure AD

```bash
# Update hosts file (Windows: C:\Windows\System32\drivers\etc\hosts)
127.0.0.1 local.docn.com

# Run with SSL
dotnet run --urls "https://local.docn.com:5211"
```

### 2. Test Login Flow

1. Navigate to `https://local.docn.com:5211`
2. Click **Sign in**
3. Should redirect to Azure AD/Okta login
4. After successful login, should return to app
5. Verify user claims in `User.Claims`

### 3. Verify Claims

Add diagnostic endpoint:

```csharp
app.MapGet("/debug/claims", (ClaimsPrincipal user) =>
{
    return Results.Ok(user.Claims.Select(c => new { c.Type, c.Value }));
}).RequireAuthorization();
```

---

## Security Best Practices

### 1. Certificate Management

- Use certificates from trusted CA in production
- Store certificates in Azure Key Vault or similar
- Implement certificate rotation every 12 months
- Monitor certificate expiration

### 2. Token Security

- Use short-lived access tokens (15-60 minutes)
- Implement refresh tokens for long sessions
- Store tokens in secure, HTTP-only cookies
- Validate tokens on every request

### 3. Multi-Factor Authentication

Enable MFA in Azure AD/Okta:

```csharp
builder.Services.Configure<OpenIdConnectOptions>(options =>
{
    options.Events.OnRedirectToIdentityProvider = context =>
    {
        // Force MFA
        context.ProtocolMessage.SetParameter("acr_values", "mfa");
        return Task.CompletedTask;
    };
});
```

---

## Troubleshooting

### Common Issues

**Issue:** Redirect URI mismatch
```
Solution: Ensure redirect URI in app registration matches exactly (including trailing slash)
```

**Issue:** Invalid client secret
```
Solution: Regenerate client secret and update appsettings.json
```

**Issue:** Claims not mapped correctly
```
Solution: Check role mapping service and claim transformation logic
```

### Logging

Enable detailed logging:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Debug);
    logging.AddFilter("Microsoft.Identity", LogLevel.Debug);
});
```

---

## References

- [Microsoft Identity Platform](https://learn.microsoft.com/en-us/azure/active-directory/develop/)
- [Okta ASP.NET Core SDK](https://developer.okta.com/code/dotnet/aspnetcore/)
- [SAML 2.0 Overview](https://en.wikipedia.org/wiki/SAML_2.0)
