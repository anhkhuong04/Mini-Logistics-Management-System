using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserE2EFactAttribute : FactAttribute
{
    public BrowserE2EFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MINILOGISTICS_BROWSER_E2E") != "1")
            Skip = "Opt-in: build frontend, install Chromium, prepare dev cert, then set MINILOGISTICS_BROWSER_E2E=1.";
    }
}

public sealed class BrowserRealBrowserTests
{
    [BrowserE2EFact]
    [Trait("Category", "BrowserE2E")]
    public async Task RealBrowser_UsesIdentityCookiesAndIsolatedApplicationData()
    {
        var root = BrowserIdentityFactory.RepositoryRoot();
        var certDirectory = Path.Combine(root, ".agent", "https");
        using var pemCertificate = X509Certificate2.CreateFromPemFile(Path.Combine(certDirectory, "localhost.pem"), Path.Combine(certDirectory, "localhost.key"));
        // Windows Schannel needs a persisted key handle; do not write the PFX or its key to disk.
        using var certificate = X509CertificateLoader.LoadPkcs12(pemCertificate.Export(X509ContentType.Pfx), null);
        await using var factory = new BrowserIdentityFactory(enableFrontend: true);
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        using var client = factory.Client();
        var owner = await factory.AddUser("browser-owner@example.test", "Shop");
        var staff = await factory.AddUser("browser-staff@example.test", "Shop");
        await factory.AddUser("browser-other@example.test", "Shop");
        await factory.EnsureRole("Shop");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            Shop New(string name) => new(owner, name, new PhoneNumber("0901234567"),
                new Address("Private test address", "Ward", "Province", "Vietnam"), DateTimeOffset.UtcNow);
            var first = New("Browser Shop A"); var second = New("Browser Shop B");
            db.Shops.AddRange(first, second);
            db.Shipments.Add(BrowserShopShipmentReadTests.NewShipment(first.Id, owner, "ML-BROWSER-001"));
            db.ShopStaffMemberships.Add(new ShopStaffMembership(first.Id, staff, owner, ShopStaffRole.Viewer,
                ShopPermission.ViewShipments, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        var address = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var healthClient = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });
        Assert.Equal(HttpStatusCode.OK, (await healthClient.GetAsync(address + "/health/live")).StatusCode);
        var node = Environment.GetEnvironmentVariable("MINILOGISTICS_NODE") ?? "node";
        var start = new ProcessStartInfo(node) { WorkingDirectory = Path.Combine(root, "src", "MiniLogistics.Frontend"),
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("node_modules/@playwright/test/cli.js"); start.ArgumentList.Add("test");
        start.ArgumentList.Add("e2e/auth.spec.ts"); start.ArgumentList.Add("e2e/shop-context.spec.ts");
        start.ArgumentList.Add("e2e/shop-shipments.spec.ts");
        start.Environment["MINILOGISTICS_E2E_BASE_URL"] = address + "/app/";
        start.Environment["MINILOGISTICS_E2E_IDENTITY_FIXTURE"] = "1";
        start.Environment["MINILOGISTICS_E2E_PASSWORD"] = BrowserIdentityFactory.Password;
        // Prevent inherited diagnostics from placing cookie-bearing traces in artifacts.
        start.Environment["MINILOGISTICS_E2E_SENSITIVE"] = "1";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        var output = await stdout + await stderr;
        Assert.True(process.ExitCode == 0, output);
    }
}
