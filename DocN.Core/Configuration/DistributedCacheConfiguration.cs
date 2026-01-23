namespace DocN.Core.Configuration;

/// <summary>
/// Configuration for distributed caching with Redis or in-memory fallback
/// </summary>
public class DistributedCacheConfiguration
{
    /// <summary>
    /// Cache provider: "Redis" or "Memory"
    /// </summary>
    public string Provider { get; set; } = "Memory";

    /// <summary>
    /// Redis-specific configuration
    /// </summary>
    public RedisConfiguration Redis { get; set; } = new();

    /// <summary>
    /// Default cache expiration time in minutes
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 60;
}

/// <summary>
/// Redis-specific configuration
/// </summary>
public class RedisConfiguration
{
    /// <summary>
    /// Redis instance name prefix for keys
    /// </summary>
    public string InstanceName { get; set; } = "DocN:";

    /// <summary>
    /// Redis connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Whether Redis caching is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;
}
