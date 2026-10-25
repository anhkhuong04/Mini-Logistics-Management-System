using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserFileDownloadTests
{
    [Fact]
    public async Task CookieFiles_PreserveFilterMimeFilenameMaskingAndUseCaseDenials()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var owner = await factory.AddUser("file-owner@example.test", "Shop"); var staff = await factory.AddUser("file-staff@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, owner, staff);
        var shipment = BrowserShopShipmentReadTests.NewShipment(shop.Id, owner, "ML-FILE-001");
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>(); db.Shipments.Add(shipment); await db.SaveChangesAsync(); }
        await BrowserShopShipmentReadTests.Login(client, "file-owner@example.test");
        var path = $"/shop/files/shipments/export.csv?shopId={shop.Id}&trackingCode=ML-FILE-001&fromUtc=2026-10-01T00:00:00Z&toUtc=2026-10-01T23:59:59.9999999Z";
        var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType); Assert.Contains("shipments-", response.Content.Headers.ContentDisposition!.ToString());
        Assert.Contains("ML-FILE-001", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("ML-FILE-001", await client.GetStringAsync(path.Replace("ML-FILE-001", "MISSING")));
        response = await client.GetAsync($"/shop/files/shipments/{shipment.Id}/label.pdf?shopId={shop.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync()));
        Assert.Contains("label-ML-FILE-001.pdf", response.Content.Headers.ContentDisposition!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/shop/files/cod-report.csv?shopId={shop.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path.Replace(shop.Id.ToString(), Guid.NewGuid().ToString()))).StatusCode); // Legacy file contract.
        using var limited = factory.Client(); await BrowserShopShipmentReadTests.Login(limited, "file-staff@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await limited.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await limited.GetAsync($"/shop/files/shipments/{shipment.Id}/label.pdf?shopId={shop.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await limited.GetAsync($"/shop/files/cod-report.csv?shopId={shop.Id}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var membership = db.ShopStaffMemberships.Single(item => item.UserId == staff);
            membership.SetAccess(MiniLogistics.Domain.Shops.ShopStaffRole.Custom,
                MiniLogistics.Domain.Shops.ShopPermission.ViewShipments | MiniLogistics.Domain.Shops.ShopPermission.ExportData, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        var masked = await limited.GetStringAsync(path); Assert.Contains("ML-FILE-001", masked); Assert.DoesNotContain("Private Receiver", masked); Assert.DoesNotContain("0901234567", masked);
        Assert.Equal(HttpStatusCode.BadRequest, (await limited.GetAsync(path + "&receiverName=Private")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await limited.GetAsync(path + "&minCodAmount=1")).StatusCode);
    }
    [Fact]
    public async Task CsvExports_QuoteAndNeutralizeSpreadsheetFormulas()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client(); var actor = await factory.AddUser("csv-owner@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, actor); await BrowserShopShipmentReadTests.Login(client, "csv-owner@example.test"); await BrowserIdentityFactory.Csrf(client);
        var body = await BrowserShopShipmentCommandTests.Body(client); body["receiverName"] = "=2+2,\"x\"";
        var created = await client.PostAsJsonAsync($"/api/v1/app/shops/{shop.Id}/shipments", body); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var csv = await client.GetStringAsync($"/shop/files/shipments/export.csv?shopId={shop.Id}");
        Assert.Contains("\"'=2+2,\"\"x\"\"\"", csv);
        var row = new { rowNumber = 1, clientOrderCode = "=1+1", receiverName = "Receiver", receiverPhone = "bad", deliveryStreet = "Street", deliveryWard = "Ward", deliveryProvince = "Province", deliveryCountry = "Vietnam", weightKg = "1", lengthCm = "10", widthCm = "10", heightCm = "10", goodsValueAmount = "0", codAmount = "0" };
        var result = await (await client.PostAsJsonAsync($"/api/v1/app/shops/{shop.Id}/shipment-imports/confirm", new { rows = new[] { row } })).Content.ReadFromJsonAsync<JsonElement>();
        var batch = result.GetProperty("batchId").GetGuid();
        csv = await client.GetStringAsync($"/shop/files/import-batches/{batch}/errors.csv?shopId={shop.Id}");
        Assert.Contains("'=1+1", csv);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/shop/files/import-batches/{batch}/errors.csv?shopId={Guid.NewGuid()}")).StatusCode);
    }
}
