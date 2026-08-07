using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Infrastructure.PartnerApi;

public sealed class WebhookUrlPolicy : IWebhookUrlPolicy
{
    private readonly IWebhookDnsResolver _dnsResolver;
    private readonly WebhookSecurityOptions _options;

    public WebhookUrlPolicy(
        IWebhookDnsResolver dnsResolver,
        IOptions<WebhookSecurityOptions> options)
    {
        _dnsResolver = dnsResolver;
        _options = options.Value;
    }

    public async Task<Result> ValidateAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateUri(url, _options.AllowedPorts, out var uri, out var error))
        {
            return Result.Failure(ApplicationErrors.ValidationFailed(error));
        }

        try
        {
            var addresses = await ResolveAsync(uri!, _dnsResolver, cancellationToken);
            if (addresses.Length == 0)
            {
                return Result.Failure(PartnerApiErrors.WebhookUrlResolutionFailed);
            }

            if (addresses.Any(address => !IsPublicAddress(address)))
            {
                return Result.Failure(ApplicationErrors.ValidationFailed(
                    "Webhook hostname must resolve only to public IP addresses."));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            return Result.Failure(PartnerApiErrors.WebhookUrlResolutionFailed);
        }

        return Result.Success();
    }

    public static async Task<IPAddress[]> ResolveAndValidateForConnectionAsync(
        string host,
        IWebhookDnsResolver dnsResolver,
        CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(host, out var literalAddress)
            ? [literalAddress]
            : await dnsResolver.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new HttpRequestException("Webhook destination did not resolve to a public IP address.");
        }

        return addresses.Select(Normalize).Distinct().ToArray();
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        address = Normalize(address);

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.None)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.IPv6None)
            || address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.IsIPv6SiteLocal)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if ((bytes[0] & 0xE0) != 0x20)
            {
                return false;
            }

            if (bytes[0] == 0x20 && bytes[1] == 0x02)
            {
                return false;
            }

            if (bytes[0] == 0x20 && bytes[1] == 0x01)
            {
                var thirdGroup = (bytes[2] << 8) | bytes[3];
                return thirdGroup != 0x0000
                    && thirdGroup != 0x0002
                    && thirdGroup != 0x0DB8
                    && (thirdGroup < 0x0010 || thirdGroup > 0x002F);
            }

            return true;
        }

        return bytes switch
        {
            [0, ..] => false,
            [10, ..] => false,
            [100, >= 64 and <= 127, ..] => false,
            [127, ..] => false,
            [169, 254, ..] => false,
            [172, >= 16 and <= 31, ..] => false,
            [192, 0, 0, ..] => false,
            [192, 0, 2, ..] => false,
            [192, 168, ..] => false,
            [198, 18 or 19, ..] => false,
            [198, 51, 100, ..] => false,
            [203, 0, 113, ..] => false,
            [168, 63, 129, 16] => false,
            [>= 224, ..] => false,
            _ => true
        };
    }

    private static bool TryValidateUri(
        string url,
        IReadOnlyCollection<int> allowedPorts,
        out Uri? uri,
        out string error)
    {
        uri = null;
        error = "Webhook URL is invalid.";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(parsed.Host)
            || !string.IsNullOrEmpty(parsed.UserInfo)
            || !string.IsNullOrEmpty(parsed.Fragment))
        {
            error = "Webhook URL must be an absolute HTTPS URL without user information or a fragment.";
            return false;
        }

        var port = parsed.IsDefaultPort ? 443 : parsed.Port;
        if (!allowedPorts.Contains(port))
        {
            error = "Webhook URL uses a port that is not allowed.";
            return false;
        }

        var host = parsed.DnsSafeHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (!IPAddress.TryParse(host, out _) && !host.Contains('.')))
        {
            error = "Webhook URL must use a public fully-qualified hostname.";
            return false;
        }

        uri = parsed;
        return true;
    }

    private static Task<IPAddress[]> ResolveAsync(
        Uri uri,
        IWebhookDnsResolver dnsResolver,
        CancellationToken cancellationToken)
    {
        return IPAddress.TryParse(uri.DnsSafeHost, out var literalAddress)
            ? Task.FromResult(new[] { literalAddress })
            : dnsResolver.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
    }

    private static IPAddress Normalize(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}
