namespace MiniLogistics.Web.Services;

public sealed class TrustedProxyOptions
{
    public const string SectionName = "TrustedProxy";

    public string[] KnownProxies { get; init; } = [];

    public string[] KnownNetworks { get; init; } = [];

    public int ForwardLimit { get; init; } = 1;

    public string ForwardedForHeaderName { get; init; } = "X-Forwarded-For";

    public string ForwardedProtoHeaderName { get; init; } = "X-Forwarded-Proto";
}
