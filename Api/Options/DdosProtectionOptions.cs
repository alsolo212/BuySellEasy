namespace Api.Options;

/// <summary>
/// Limits that keep a single client from exhausting the API process. These are
/// an application-level backstop; volumetric attacks must still be stopped by
/// the CDN/WAF in front of the origin.
/// </summary>
public sealed class DdosProtectionOptions
{
    public const string SectionName = "DdosProtection";

    public int RequestsPerMinute { get; init; } = 300;
    public int BurstSize { get; init; } = 60;
    public int MaxConcurrentConnections { get; init; } = 2048;
    public int MaxConcurrentUpgradedConnections { get; init; } = 256;
    public int RequestHeadersTimeoutSeconds { get; init; } = 15;
    public int KeepAliveTimeoutSeconds { get; init; } = 120;
    public int MinimumRequestBodyBytesPerSecond { get; init; } = 240;
    public long MaxRequestBodyBytes { get; init; } = 32 * 1024 * 1024;
}
