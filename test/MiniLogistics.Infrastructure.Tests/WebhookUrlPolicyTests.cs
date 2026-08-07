using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Infrastructure.PartnerApi;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class WebhookUrlPolicyTests
{
    [Theory]
    [InlineData("http://partner.example.com/webhook")]
    [InlineData("https://localhost/webhook")]
    [InlineData("https://internal/webhook")]
    [InlineData("https://user:password@partner.example.com/webhook")]
    [InlineData("https://partner.example.com/webhook#ignored-fragment")]
    [InlineData("https://partner.example.com:8443/webhook")]
    public async Task ValidateAsync_RejectsInvalidUrlShape(string url)
    {
        var policy = CreatePolicy(IPAddress.Parse("8.8.8.8"));

        var result = await policy.ValidateAsync(url);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.1.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("168.63.129.16")]
    [InlineData("192.0.2.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("100::1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("2001::1")]
    [InlineData("2001:2::1")]
    [InlineData("2001:20::1")]
    [InlineData("2002:7f00:1::1")]
    [InlineData("2001:db8::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task ValidateAsync_RejectsNonPublicAddress(string address)
    {
        var policy = CreatePolicy(IPAddress.Parse(address));

        var result = await policy.ValidateAsync("https://partner.example.com/webhook");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ValidateAsync_RejectsHostnameWithMixedPublicAndPrivateAnswers()
    {
        var policy = CreatePolicy(
            IPAddress.Parse("8.8.8.8"),
            IPAddress.Parse("10.0.0.5"));

        var result = await policy.ValidateAsync("https://partner.example.com/webhook");

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public async Task ValidateAsync_AcceptsPublicHttpsDestination(string address)
    {
        var policy = CreatePolicy(IPAddress.Parse(address));

        var result = await policy.ValidateAsync("https://partner.example.com/webhook");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("https://2130706433/webhook")]
    [InlineData("https://0x7f000001/webhook")]
    public async Task ValidateAsync_RejectsEncodedLoopbackLiteral(string url)
    {
        var policy = CreatePolicy(IPAddress.Parse("8.8.8.8"));

        var result = await policy.ValidateAsync(url);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ConnectValidation_RejectsPrivateAnswerReturnedAfterRegistration()
    {
        var resolver = new FakeDnsResolver([IPAddress.Parse("10.0.0.7")]);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            WebhookUrlPolicy.ResolveAndValidateForConnectionAsync(
                "partner.example.com",
                resolver,
                CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_WhenDnsTemporarilyFails_ReturnsRetryableResolutionError()
    {
        var policy = new WebhookUrlPolicy(
            new ThrowingDnsResolver(),
            Options.Create(new WebhookSecurityOptions()));

        var result = await policy.ValidateAsync("https://partner.example.com/webhook");

        Assert.True(result.IsFailure);
        Assert.Equal(PartnerApiErrors.WebhookUrlResolutionFailed.Code, result.Error.Code);
    }

    [Fact]
    public void HttpHandler_DisablesRedirectsAndBoundsConnectionsAndHeaders()
    {
        var handler = WebhookHttpMessageHandler.Create(
            new FakeDnsResolver([IPAddress.Parse("8.8.8.8")]),
            Options.Create(new WebhookSecurityOptions()));

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.False(handler.UseCookies);
        Assert.Equal(8, handler.MaxConnectionsPerServer);
        Assert.Equal(16, handler.MaxResponseHeadersLength);
    }

    private static WebhookUrlPolicy CreatePolicy(params IPAddress[] addresses)
    {
        return new WebhookUrlPolicy(
            new FakeDnsResolver(addresses),
            Options.Create(new WebhookSecurityOptions()));
    }

    private sealed class FakeDnsResolver : IWebhookDnsResolver
    {
        private readonly IPAddress[] _addresses;

        public FakeDnsResolver(IPAddress[] addresses)
        {
            _addresses = addresses;
        }

        public Task<IPAddress[]> GetHostAddressesAsync(
            string host,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_addresses);
        }
    }

    private sealed class ThrowingDnsResolver : IWebhookDnsResolver
    {
        public Task<IPAddress[]> GetHostAddressesAsync(
            string host,
            CancellationToken cancellationToken = default)
        {
            throw new SocketException((int)SocketError.TryAgain);
        }
    }
}
