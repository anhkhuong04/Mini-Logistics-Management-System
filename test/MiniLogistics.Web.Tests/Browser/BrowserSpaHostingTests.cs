using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserSpaHostingTests
{
    private const string ReactMarker = "react-hosting-test";

    [Theory]
    [InlineData("/app")]
    [InlineData("/app/")]
    [InlineData("/app/dashboard?shopId=example")]
    public async Task EnabledSpa_RefreshesDeepLinksWithoutTakingLegacyRoutes(string path)
    {
        using var factory = new BrowserSpaFactory(enabled: true);
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ReactMarker, await response.Content.ReadAsStringAsync());
        Assert.Contains("no-cache", response.Headers.CacheControl?.ToString());

        using var legacy = await client.GetAsync("/dashboard");
        Assert.DoesNotContain(ReactMarker, await legacy.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DisabledSpa_DoesNotExposeFrontendOrRuntimeConfig()
    {
        using var factory = new BrowserSpaFactory(enabled: false);
        using var client = factory.CreateHttpsClient();
        foreach (var path in new[] { "/app", "/frontend-config.js", "/frontend-assets/entry-abc123.js" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(ReactMarker, await response.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData("/api/v1/app/missing")]
    [InlineData("/api/v1/partner/missing")]
    [InlineData("/auth/missing")]
    [InlineData("/shop/files/missing")]
    [InlineData("/culture/missing")]
    [InlineData("/health/missing")]
    [InlineData("/openapi/missing")]
    [InlineData("/frontend-assets/missing.js")]
    [InlineData("/app/missing.js")]
    public async Task ReservedAndMissingAssetPaths_NeverReturnAnHtmlFallback(string path)
    {
        using var factory = new BrowserSpaFactory(enabled: true);
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(ReactMarker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RuntimeConfig_ContainsOnlyPublicPaths_AndAssetsAreServedSeparately()
    {
        using var factory = new BrowserSpaFactory(enabled: true);
        using var client = factory.CreateHttpsClient();
        var config = await client.GetStringAsync("/frontend-config.js");
        const string prefix = "window.__MINI_LOGISTICS_CONFIG__ = ";
        Assert.StartsWith(prefix, config);
        using var json = JsonDocument.Parse(config[prefix.Length..].TrimEnd(';', '\n', '\r'));
        Assert.Equal(2, json.RootElement.EnumerateObject().Count());
        Assert.Equal("/app", json.RootElement.GetProperty("basePath").GetString());
        Assert.Equal("/frontend-assets/", json.RootElement.GetProperty("assetBasePath").GetString());
        using var asset = await client.GetAsync("/frontend-assets/entry-abc123.js");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Contains("asset-test", await asset.Content.ReadAsStringAsync());
        Assert.Contains("immutable", asset.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task MissingBundle_ReturnsUnavailableInsteadOfStartingARazorCircuit()
    {
        using var factory = new BrowserSpaFactory(enabled: true, includeIndex: false);
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/app/dashboard");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("blazor", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostToFrontend_DoesNotReturnIndexHtml()
    {
        using var factory = new BrowserSpaFactory(enabled: true);
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsync("/app/dashboard", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.DoesNotContain(ReactMarker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void BrowserTestEnvironment_DoesNotRegisterInfrastructureWorkers()
    {
        using var factory = new BrowserSpaFactory(enabled: true, environment: "BrowserTests");
        using var client = factory.CreateHttpsClient();
        Assert.DoesNotContain(factory.Services.GetServices<IHostedService>(), service =>
            service.GetType().Assembly.GetName().Name == "MiniLogistics.Infrastructure");
    }

    private sealed class BrowserSpaFactory : WebApplicationFactory<Program>
    {
        private readonly bool _enabled;
        private readonly string _environment;
        private readonly string _distPath = Path.Combine(Path.GetTempPath(), "MiniLogisticsSpaTests", Guid.NewGuid().ToString("N"));

        public BrowserSpaFactory(bool enabled, bool includeIndex = true, string environment = "Development")
        {
            _enabled = enabled;
            _environment = environment;
            Directory.CreateDirectory(_distPath);
            if (includeIndex)
            {
                File.WriteAllText(Path.Combine(_distPath, "index.html"),
                    $"<!doctype html><html lang=\"vi\"><body><div id=\"root\">{ReactMarker}</div><script src=\"/frontend-config.js\"></script></body></html>");
            }
            File.WriteAllText(Path.Combine(_distPath, "entry-abc123.js"), "console.log('asset-test');");
        }

        public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniLogisticsBrowserHostingTests;Trusted_Connection=True;TrustServerCertificate=True");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["BrowserSpa:Enabled"] = _enabled.ToString(),
                    ["BrowserSpa:DistPath"] = _distPath
                }));
            if (_environment != "BrowserTests")
            {
                builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_distPath))
            {
                Directory.Delete(_distPath, recursive: true);
            }
        }
    }
}
