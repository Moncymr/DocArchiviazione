namespace DocN.Server.Middleware;

/// <summary>
/// Middleware to add security headers for protection against common web vulnerabilities
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent clickjacking attacks
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        
        // Prevent MIME type sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        
        // Enable XSS protection (for older browsers)
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        
        // Force HTTPS for all future requests
        context.Response.Headers.Append("Strict-Transport-Security", 
            "max-age=31536000; includeSubDomains; preload");
        
        // Content Security Policy - restrict resource loading
        // Note: 'unsafe-inline' and 'unsafe-eval' are required for Blazor Server functionality.
        // For enhanced security in pure API scenarios, consider removing these directives.
        // For production, implement nonce-based CSP or migrate to Blazor WebAssembly.
        var csp = "default-src 'self'; " +
                  "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                  "style-src 'self' 'unsafe-inline'; " +
                  "img-src 'self' data: https:; " +
                  "font-src 'self' data:; " +
                  "connect-src 'self' https://api.openai.com https://generativelanguage.googleapis.com https://*.openai.azure.com; " +
                  "frame-ancestors 'none'; " +
                  "base-uri 'self'; " +
                  "form-action 'self';";
        context.Response.Headers.Append("Content-Security-Policy", csp);
        
        // Referrer Policy - control referrer information
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Permissions Policy - disable unused browser features
        context.Response.Headers.Append("Permissions-Policy", 
            "camera=(), microphone=(), geolocation=(), payment=()");
        
        _logger.LogTrace("Security headers added to response");
        
        await _next(context);
    }
}
