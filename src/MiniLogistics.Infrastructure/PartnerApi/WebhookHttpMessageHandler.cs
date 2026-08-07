using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace MiniLogistics.Infrastructure.PartnerApi;

public static class WebhookHttpMessageHandler
{
    public static SocketsHttpHandler Create(
        IWebhookDnsResolver dnsResolver,
        IOptions<WebhookSecurityOptions> optionsAccessor)
    {
        var options = optionsAccessor.Value;
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds),
            MaxResponseHeadersLength = options.MaxResponseHeadersKilobytes,
            MaxConnectionsPerServer = options.MaxConnectionsPerServer,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await WebhookUrlPolicy.ResolveAndValidateForConnectionAsync(
                    context.DnsEndPoint.Host,
                    dnsResolver,
                    cancellationToken);
                Exception? lastException = null;

                foreach (var address in addresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(
                            address,
                            context.DnsEndPoint.Port,
                            cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        socket.Dispose();
                        throw;
                    }
                    catch (SocketException exception)
                    {
                        socket.Dispose();
                        lastException = exception;
                    }
                }

                throw new HttpRequestException("Unable to connect to the validated webhook destination.", lastException);
            }
        };
    }
}
