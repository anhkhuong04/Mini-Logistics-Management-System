namespace MiniLogistics.Infrastructure.PartnerApi;

public sealed class WebhookSecurityOptions
{
    public const string SectionName = "PartnerApi:WebhookSecurity";

    public int[] AllowedPorts { get; init; } = [443];

    public int ConnectTimeoutSeconds { get; init; } = 5;

    public int RequestTimeoutSeconds { get; init; } = 10;

    public int MaxResponseBodyBytes { get; init; } = 2048;

    public int MaxResponseHeadersKilobytes { get; init; } = 16;

    public int MaxConnectionsPerServer { get; init; } = 8;
}
