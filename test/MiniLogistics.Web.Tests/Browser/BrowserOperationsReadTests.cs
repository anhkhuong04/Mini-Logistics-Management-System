using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Operations;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserOperationsReadTests
{
    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("Operator", HttpStatusCode.OK)]
    [InlineData("Shop", HttpStatusCode.Forbidden)]
    [InlineData("Shipper", HttpStatusCode.Forbidden)]
    public async Task Reads_EnforceApplicationPermissions(string role, HttpStatusCode expected)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var email = $"ops-{role}@example.test"; var actor = await factory.AddUser(email, role);
        using (var scope = factory.Services.CreateScope())
        {
            var useCase = scope.ServiceProvider.GetRequiredService<IOperationsReadService>();
            Assert.Equal(expected == HttpStatusCode.OK, (await useCase.ShippersAsync(actor, default)).IsSuccess);
        }
        await BrowserShopShipmentReadTests.Login(client, email);
        foreach (var path in new[] { "shipments", "pending-pickups", "shippers" })
            Assert.Equal(expected, (await client.GetAsync("/api/v1/app/operations/" + path)).StatusCode);
        var cod = await client.GetAsync("/api/v1/app/operations/cod/settlement-candidates");
        Assert.Equal(role == "Admin" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, cod.StatusCode);
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("pageNumber=0")]
    [InlineData("pageNumber=2147483647")]
    [InlineData("status=3")]
    [InlineData("status=Assigned&status=Delivered")]
    [InlineData("minCodAmount=1e3")]
    [InlineData("minCodAmount=2&maxCodAmount=1")]
    public async Task ReadQuery_RejectsInvalidAndDuplicateFilters(string query)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        await factory.AddUser("ops-query@example.test", "Admin"); await BrowserShopShipmentReadTests.Login(client, "ops-query@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/app/operations/shipments?" + query)).StatusCode);
    }
}
