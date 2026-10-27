using System.Text.Json;
using MiniLogistics.Application.PartnerApi;
namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserIntegrationClientEndpoints
{
    public static void MapBrowserIntegrationClients(this RouteGroupBuilder group)
    {
        group.MapGet("/integrations/dashboard", async (HttpContext c, Guid? shopId, IWebhookManagementService service, IApiCredentialPolicy policy) =>
        {
            BrowserApiEndpoints.RequireUniqueQuery(c);
            var result = await service.GetDashboardAsync(BrowserUserClaims.GetUserId(c), c.RequestAborted);
            if (result.IsFailure)
                return BrowserApiResultMapper.Problem(c, result.Error.Code);
            if (shopId.HasValue && !result.Value.Shops.Any(s => s.ShopId == shopId))
                return BrowserApiResultMapper.Problem(c, "Application.Forbidden");
            return BrowserApiResultMapper.Json(BrowserIntegrationContracts.Response(result.Value, shopId, policy.ApiKeyPrefix));
        }).WithName("BrowserIntegrationDashboard").Produces<BrowserIntegrationDashboardResponse>();
        group.MapPost("/integrations/clients", async (HttpContext c, IApiClientManagementService service) =>
        {
            var b = await BrowserApiEndpoints.ReadAsync<BrowserIntegrationCreateRequest>(c);
            BrowserIntegrationContracts.Ips(b.AllowedIpAddresses);
            if (b.Name is null || b.Name.Length > 150)
                throw new JsonException();
            return BrowserApiResultMapper.From(c, await service.CreateApiClientAsync(new(BrowserUserClaims.GetUserId(c), b.ShopId, b.Name, BrowserIntegrationContracts.Scopes(b.Scopes), b.AllowedIpAddresses, b.ExpiresAtUtc), c.RequestAborted));
        }).WithName("BrowserCreateIntegrationClient").Accepts<BrowserIntegrationCreateRequest>("application/json").Produces<PartnerApiClientSecretResponse>();
        group.MapPost("/integrations/clients/{id:guid}/rotate-key", async (HttpContext c, Guid id, IApiClientManagementService service) => BrowserApiResultMapper.From(c, await service.RotateApiClientKeyAsync(new(BrowserUserClaims.GetUserId(c), id), c.RequestAborted)))
            .WithName("BrowserRotateIntegrationKey").Produces<PartnerApiClientSecretResponse>();
        group.MapPut("/integrations/clients/{id:guid}/active", async (HttpContext c, Guid id, IApiClientManagementService service) =>
        {
            var b = await BrowserApiEndpoints.ReadAsync<BrowserIntegrationActiveRequest>(c);
            if (b.IsActive is null)
                throw new JsonException();
            return BrowserApiResultMapper.Empty(c, await service.SetApiClientActiveStatusAsync(new(BrowserUserClaims.GetUserId(c), id, b.IsActive.Value), c.RequestAborted));
        }).WithName("BrowserSetIntegrationActive").Accepts<BrowserIntegrationActiveRequest>("application/json").Produces(204);
        group.MapPut("/integrations/clients/{id:guid}/security", async (HttpContext c, Guid id, IApiClientManagementService service) =>
        {
            var b = await BrowserApiEndpoints.ReadAsync<BrowserIntegrationSecurityRequest>(c);
            BrowserIntegrationContracts.Ips(b.AllowedIpAddresses);
            return BrowserApiResultMapper.Empty(c, await service.UpdateApiClientSecurityAsync(new(BrowserUserClaims.GetUserId(c), id, BrowserIntegrationContracts.Scopes(b.Scopes), b.AllowedIpAddresses, b.ExpiresAtUtc), c.RequestAborted));
        }).WithName("BrowserSetIntegrationSecurity").Accepts<BrowserIntegrationSecurityRequest>("application/json").Produces(204);
    }
}
