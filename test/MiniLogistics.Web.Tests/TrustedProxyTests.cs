using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class TrustedProxyTests
{
    [Fact]
    public async Task ForwardedHeaders_TrustedProxyUsesOriginalClientAddress()
    {
        var context = CreateContext(
            remoteAddress: "10.0.0.10",
            forwardedFor: "203.0.113.20");
        var middleware = CreateMiddleware(IPAddress.Parse("10.0.0.10"));

        await middleware.Invoke(context);

        Assert.Equal(IPAddress.Parse("203.0.113.20"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ForwardedHeaders_UntrustedSourceCannotSpoofClientAddress()
    {
        var directAddress = IPAddress.Parse("198.51.100.15");
        var context = CreateContext(
            directAddress.ToString(),
            forwardedFor: "203.0.113.20");
        var middleware = CreateMiddleware(IPAddress.Parse("10.0.0.10"));

        await middleware.Invoke(context);

        Assert.Equal(directAddress, context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ForwardedHeaders_ForwardLimitProcessesOnlyNearestHop()
    {
        var context = CreateContext(
            remoteAddress: "10.0.0.10",
            forwardedFor: "203.0.113.20, 10.0.0.11");
        var middleware = CreateMiddleware(
            IPAddress.Parse("10.0.0.10"),
            IPAddress.Parse("10.0.0.11"));

        await middleware.Invoke(context);

        Assert.Equal(IPAddress.Parse("10.0.0.11"), context.Connection.RemoteIpAddress);
    }

    private static DefaultHttpContext CreateContext(string remoteAddress, string forwardedFor)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context;
    }

    private static ForwardedHeadersMiddleware CreateMiddleware(params IPAddress[] knownProxies)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
            ForwardLimit = 1
        };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var knownProxy in knownProxies)
        {
            options.KnownProxies.Add(knownProxy);
        }

        return new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options));
    }
}
