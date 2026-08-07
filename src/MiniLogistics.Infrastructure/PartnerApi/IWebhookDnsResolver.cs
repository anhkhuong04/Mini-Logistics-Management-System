using System.Net;

namespace MiniLogistics.Infrastructure.PartnerApi;

public interface IWebhookDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(
        string host,
        CancellationToken cancellationToken = default);
}

internal sealed class SystemWebhookDnsResolver : IWebhookDnsResolver
{
    public Task<IPAddress[]> GetHostAddressesAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        return Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}
