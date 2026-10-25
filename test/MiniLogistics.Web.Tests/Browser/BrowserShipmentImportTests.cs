using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniLogistics.Web.Services;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserShipmentImportTests
{
    [Fact]
    public async Task PreviewOpenApi_DescribesNamedMultipartFilePart()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/browser-v1.json");
        var schema = document.GetProperty("paths").GetProperty("/api/v1/app/shops/{shopId}/shipment-imports/preview").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
        var name = schema.GetProperty("$ref").GetString()!.Split('/').Last();
        Assert.True(document.GetProperty("components").GetProperty("schemas").GetProperty(name).GetProperty("properties").TryGetProperty("file", out _));
    }
    internal const string Headers = "clientOrderCode,receiverName,receiverPhone,deliveryStreet,deliveryWard,deliveryProvince,deliveryCountry,weightKg,lengthCm,widthCm,heightCm,goodsValueAmount,codAmount,note";
    internal static async Task<string> Csv(HttpClient client, string code = "IMPORT-1")
    {
        var input = await BrowserShippingQuoteTests.Request(client);
        return Headers + $"\n{code},Receiver,0901234567,Street,{input.DeliveryAddress.Ward},{input.DeliveryAddress.Province},Vietnam,1,10,10,10,0,10000,Test";
    }
    internal static MultipartFormDataContent File(byte[] content, string name = "import.csv", string mime = "text/csv")
    {
        var form = new MultipartFormDataContent(); var file = new ByteArrayContent(content);
        file.Headers.ContentType = new(mime); form.Add(file, "file", name); return form;
    }
    [Fact]
    public async Task PreviewConfirmAndBatch_AreCookieScopedRevalidatedAndQueued()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var owner = await factory.AddUser("import-owner@example.test", "Shop"); var staff = await factory.AddUser("import-staff@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, owner, staff); var csv = await Csv(client);
        var path = $"/api/v1/app/shops/{shop.Id}/shipment-imports";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(path + "/preview", File(Encoding.UTF8.GetBytes(csv)))).StatusCode);
        await BrowserShopShipmentReadTests.Login(client, "import-owner@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path + "/preview", File(Encoding.UTF8.GetBytes(csv)))).StatusCode);
        await BrowserIdentityFactory.Csrf(client);
        var response = await client.PostAsync(path + "/preview", File(Encoding.UTF8.GetBytes(csv)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(1, preview.GetProperty("validRows").GetInt32());
        var row = JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(preview.GetProperty("rows")[0].GetProperty("draft").GetRawText())!;
        row["receiverPhone"] = "invalid"; // Tampered after preview: confirm must not trust IsValid or fee.
        response = await client.PostAsJsonAsync(path + "/confirm", new { rows = new[] { row }, currentUserId = Guid.NewGuid(), isValid = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(1, result.GetProperty("failedRows").GetInt32());
        var batchId = result.GetProperty("batchId").GetGuid(); Assert.Equal("CompletedWithErrors", result.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "/" + batchId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/app/shops/{Guid.NewGuid()}/shipment-imports/{batchId}")).StatusCode);
        row["receiverPhone"] = "0901234567";
        result = await (await client.PostAsJsonAsync(path + "/confirm", new { rows = new[] { row } })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", result.GetProperty("status").GetString());
        var pendingId = result.GetProperty("batchId").GetGuid();
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            Assert.All(db.ShipmentImportBatches, batch => Assert.Equal(owner, batch.RequestedByUserId));
            Assert.Empty(db.Shipments);
        }
        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<MiniLogistics.Application.Shipments.ImportShipments.ShipmentImportBatchProcessor>().ProcessNextRowAsync());
        result = await client.GetFromJsonAsync<JsonElement>(path + "/" + pendingId);
        Assert.Equal("Completed", result.GetProperty("status").GetString()); Assert.Equal(1, result.GetProperty("createdRows").GetInt32());
        var duplicateCsv = csv + "\n" + csv.Split('\n')[1];
        preview = await (await client.PostAsync(path + "/preview", File(Encoding.UTF8.GetBytes(duplicateCsv)))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, preview.GetProperty("invalidRows").GetInt32());
        using var other = factory.Client(); await BrowserShopShipmentReadTests.Login(other, "import-staff@example.test"); await BrowserIdentityFactory.Csrf(other);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync(path + "/preview", File(Encoding.UTF8.GetBytes(csv)))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync(path + "/confirm", new { rows = new[] { row } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(path + "/" + batchId)).StatusCode);
    }
    [Fact]
    public async Task Preview_IdentifiesInvalidNumericCellsWithoutSilentlyTreatingThemAsRealZero()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client(); var owner = await factory.AddUser("numeric-import@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, owner); await BrowserShopShipmentReadTests.Login(client, "numeric-import@example.test"); await BrowserIdentityFactory.Csrf(client);
        var csv = (await Csv(client)).Replace(",0,10000,Test", ",bad,bad,Test");
        var preview = await (await client.PostAsync($"/api/v1/app/shops/{shop.Id}/shipment-imports/preview", File(Encoding.UTF8.GetBytes(csv)))).Content.ReadFromJsonAsync<JsonElement>();
        var fields = preview.GetProperty("rows")[0].GetProperty("invalidNumericFields").EnumerateArray().Select(value => value.GetString()).ToList();
        Assert.Contains("codAmount", fields); Assert.Contains("goodsValueAmount", fields);
    }
    [Theory]
    [InlineData("oversize", 413)]
    [InlineData("utf8", 400)]
    [InlineData("binary", 400)]
    [InlineData("extension", 400)]
    [InlineData("mime", 415)]
    public async Task Upload_RejectsUnsafeInputBeforeUseCase(string kind, int status)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        await factory.AddUser("upload@example.test", "Shop"); await BrowserShopShipmentReadTests.Login(client, "upload@example.test"); await BrowserIdentityFactory.Csrf(client);
        var bytes = kind switch { "oversize" => new byte[2_000_001], "utf8" => new byte[] { 0xff, 0xfe, 0xfd }, "binary" => new byte[] { 0, 1, 2 }, _ => Encoding.UTF8.GetBytes(Headers) };
        var response = await client.PostAsync($"/api/v1/app/shops/{Guid.NewGuid()}/shipment-imports/preview", File(bytes, kind == "extension" ? "file.exe" : "file.csv", kind == "mime" ? "image/png" : "text/csv"));
        Assert.Equal(status, (int)response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
    [Theory]
    [InlineData("preview", ShopUiActionKind.ImportPreview)]
    [InlineData("confirm", ShopUiActionKind.ImportConfirm)]
    public async Task ImportIngress_KeepsActorLimiterAndRetryAfter(string operation, ShopUiActionKind expected)
    {
        var limiter = new Denied(expected);
        await using var factory = new BrowserIdentityFactory(configureServices: services => { services.RemoveAll<IShopUiActionRateLimiter>(); services.AddSingleton<IShopUiActionRateLimiter>(limiter); });
        using var client = factory.Client(); var actor = await factory.AddUser("rate-import@example.test", "Shop");
        await BrowserShopShipmentReadTests.Login(client, "rate-import@example.test"); await BrowserIdentityFactory.Csrf(client);
        var path = $"/api/v1/app/shops/{Guid.NewGuid()}/shipment-imports/{operation}";
        var response = operation == "preview" ? await client.PostAsync(path, File(Encoding.UTF8.GetBytes(Headers)))
            : await client.PostAsJsonAsync(path, new { rows = new[] { new { rowNumber = 1, receiverName = "Receiver", receiverPhone = "0901234567", deliveryStreet = "Street", deliveryWard = "Ward", deliveryProvince = "Province", deliveryCountry = "Vietnam", weightKg = "1", lengthCm = "10", widthCm = "10", heightCm = "10", goodsValueAmount = "0", codAmount = "0" } } });
        Assert.Equal(429, (int)response.StatusCode); Assert.Equal("12", response.Headers.GetValues("Retry-After").Single()); Assert.Equal(actor, limiter.Actor);
    }
    private sealed class Denied(ShopUiActionKind expected) : IShopUiActionRateLimiter
    {
        public Guid Actor;
        public bool TryAcquire(Guid userId, ShopUiActionKind kind, out TimeSpan retryAfter)
        { Assert.Equal(expected, kind); Actor = userId; retryAfter = TimeSpan.FromSeconds(12); return false; }
    }
    [Fact]
    public async Task ImportLimits_AllowFileAboveFoundationLimitButRejectMalformedRowIdentity()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client(); var owner = await factory.AddUser("limits-import@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, owner); await BrowserShopShipmentReadTests.Login(client, "limits-import@example.test"); await BrowserIdentityFactory.Csrf(client);
        var path = $"/api/v1/app/shops/{shop.Id}/shipment-imports";
        var response = await client.PostAsync(path + "/preview", File(Encoding.UTF8.GetBytes(await Csv(client) + new string('x', 70_000))));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Row invalid, transport valid above64KiB.
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invalidRows").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/confirm", new { rows = new object?[] { null } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/confirm", new { rows = new[] { new { rowNumber = 0 } } })).StatusCode);
    }
}
