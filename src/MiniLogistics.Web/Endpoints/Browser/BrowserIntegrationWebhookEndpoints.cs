using System.Text.Json;
using MiniLogistics.Application.PartnerApi;
namespace MiniLogistics.Web.Endpoints.Browser;
public static class BrowserIntegrationWebhookEndpoints
{
    public static void MapBrowserIntegrationWebhooks(this RouteGroupBuilder group) {
        group.MapPut("/integrations/clients/{id:guid}/webhook",async(HttpContext c,Guid id,IWebhookManagementService service)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserIntegrationWebhookRequest>(c);
            if(b.Url is null||b.Url.Length>500||b.SigningSecret is null||b.SigningSecret.Length>200)throw new JsonException();
            return BrowserApiResultMapper.From(c,await service.UpsertWebhookEndpointAsync(new(BrowserIntegrationClientEndpoints.Actor(c),id,b.Url,b.SigningSecret),c.RequestAborted));
        }).WithName("BrowserSetIntegrationWebhook").Accepts<BrowserIntegrationWebhookRequest>("application/json").Produces<PartnerWebhookEndpointResponse>();
        group.MapPost("/integrations/clients/{id:guid}/webhook/test",async(HttpContext c,Guid id,IWebhookManagementService service)=>BrowserApiResultMapper.From(c,await service.TestWebhookAsync(new(BrowserIntegrationClientEndpoints.Actor(c),id),c.RequestAborted)))
            .WithName("BrowserTestIntegrationWebhook").Produces<PartnerWebhookTestResponse>();
        group.MapPost("/integrations/deliveries/{id:guid}/retry",async(HttpContext c,Guid id,IWebhookManagementService service)=>BrowserOperationsAssignmentEndpoints.Empty(c,await service.RetryWebhookDeliveryAsync(new(BrowserIntegrationClientEndpoints.Actor(c),id),c.RequestAborted)))
            .WithName("BrowserRetryIntegrationDelivery").Produces(204);
        group.MapPost("/integrations/outbox/{id:guid}/retry",async(HttpContext c,Guid id,IWebhookManagementService service)=>BrowserOperationsAssignmentEndpoints.Empty(c,await service.RetryOutboxMessageAsync(new(BrowserIntegrationClientEndpoints.Actor(c),id),c.RequestAborted)))
            .WithName("BrowserRetryIntegrationOutbox").Produces(204);
    }
}
