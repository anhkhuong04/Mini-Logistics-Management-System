using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserRootRoutingTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/tracking?code=ML-TEST")]
    [InlineData("/admin/dashboard")]
    [InlineData("/operations/assignment")]
    [InlineData("/shipments/create?shipmentId=11111111-1111-1111-1111-111111111111")]
    [InlineData("/not-a-known-route")]
    public async Task RootHasSingleReactOwnerAndRuntimeBase(string path)
    {
        await using var factory = new RootFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("root-test", html);
        Assert.Contains("\"basePath\":\"\"", await client.GetStringAsync("/frontend-config.js"));
        Assert.DoesNotContain("blazor", html, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("/api/missing")]
    [InlineData("/auth/missing")]
    [InlineData("/frontend-assets/missing.js")]
    [InlineData("/images/missing")]
    [InlineData("/data/missing")]
    [InlineData("/_blazor")]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/health/missing")]
    [InlineData("/openapi/missing")]
    public async Task ReservedPathsNeverReturnSpa(string path)
    {
        await using var factory = new RootFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("root-test", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OldAppLinksRedirectLocallyWithQueryAndMutationsNeverReturnHtml()
    {
        await using var factory = new RootFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/app/shipments?shopId=abc&status=Delivered");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/shipments?shopId=abc&status=Delivered", response.Headers.Location?.OriginalString);
        Assert.Equal("/?code=ML-TEST", (await client.GetAsync("/app?code=ML-TEST")).Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/app")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsync("/dashboard", new StringContent("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    internal sealed class RootFactory : WebApplicationFactory<Program>
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "MiniLogisticsRoot", Guid.NewGuid().ToString("N"));
        public RootFactory()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "index.html"), "<!doctype html><html lang=\"vi\"><body><div id=\"root\">root-test</div></body></html>");
        }

        public HttpClient Client() => CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("BrowserTests");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(localdb)\\MSSQLLocalDB;Database=UnusedRoot;Trusted_Connection=True");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BrowserSpa:Enabled"] = "true",
                ["BrowserSpa:ServeAtRoot"] = "true",
                ["BrowserSpa:DistPath"] = _root,
                ["BannerImages:RootPath"] = Path.Combine(Path.GetTempPath(), "MiniLogisticsRootImages")
            }));
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
