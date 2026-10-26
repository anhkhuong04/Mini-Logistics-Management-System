using System.Text.Json;
using MiniLogistics.Application.AdminHubs;
using MiniLogistics.Application.AdminHubs.GetAdminHubs;
using MiniLogistics.Application.AdminHubs.CreateHub;
using MiniLogistics.Application.AdminHubs.UpdateHub;
using MiniLogistics.Application.AdminHubs.SetHubActiveStatus;
namespace MiniLogistics.Web.Endpoints.Browser;
public static class BrowserAdminHubEndpoints
{
    public static void MapBrowserAdminHubs(this RouteGroupBuilder group) {
        group.MapGet("/admin/hubs",async(HttpContext c,[AsParameters]BrowserAdminQuery q,IGetAdminHubsService s)=>{
            q.Validate(c); return BrowserApiResultMapper.From(c,await s.GetAsync(new(BrowserAdminReportingEndpoints.Actor(c),q.SearchText,q.IsActive,q.Province,q.IsRegionalSortingHub),c.RequestAborted));
        }).WithName("BrowserAdminHubs").Produces<IReadOnlyList<AdminHubResponse>>();
        group.MapPost("/admin/hubs",async(HttpContext c,ICreateHubService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserAdminHubRequest>(c);
            return BrowserApiResultMapper.From(c,await s.CreateAsync(new(BrowserAdminReportingEndpoints.Actor(c),b.Code,b.Name,b.Province,b.Ward,b.AddressLine,b.Country,b.IsRegionalSortingHub),c.RequestAborted));
        }).WithName("BrowserCreateHub").Accepts<BrowserAdminHubRequest>("application/json").Produces<AdminHubResponse>();
        group.MapPut("/admin/hubs/{id:guid}",async(HttpContext c,Guid id,IUpdateHubService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserAdminHubRequest>(c);
            return BrowserApiResultMapper.From(c,await s.UpdateAsync(new(BrowserAdminReportingEndpoints.Actor(c),id,b.Code,b.Name,b.Province,b.Ward,b.AddressLine,b.Country,b.IsRegionalSortingHub),c.RequestAborted));
        }).WithName("BrowserUpdateHub").Accepts<BrowserAdminHubRequest>("application/json").Produces<AdminHubResponse>();
        group.MapPut("/admin/hubs/{id:guid}/active",async(HttpContext c,Guid id,ISetHubActiveStatusService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserAdminActiveRequest>(c); if(b.IsActive is null)throw new JsonException();
            return BrowserOperationsAssignmentEndpoints.Empty(c,await s.SetAsync(new(BrowserAdminReportingEndpoints.Actor(c),id,b.IsActive.Value,b.Reason),c.RequestAborted));
        }).WithName("BrowserSetHubActive").Accepts<BrowserAdminActiveRequest>("application/json").Produces(204);
    }
}
