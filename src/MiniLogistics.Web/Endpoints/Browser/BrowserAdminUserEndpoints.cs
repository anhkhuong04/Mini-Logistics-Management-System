using System.Text.Json;
using MiniLogistics.Application.AdminUsers.GetAdminUsers;
using MiniLogistics.Application.AdminUsers.CreateInternalUser;
using MiniLogistics.Application.AdminUsers.SetUserActiveStatus;
using MiniLogistics.Application.AdminUsers.ResetUserPassword;
using MiniLogistics.Application.AdminUsers.SetShipperCapacity;
using MiniLogistics.Application.Shippers.SetShipperWorkingAreas;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Application.Common;

namespace MiniLogistics.Web.Endpoints.Browser;
public sealed record BrowserAdminAreasRequest(IReadOnlyList<SetShipperWorkingAreaItem> Areas);
public static class BrowserAdminUserEndpoints
{
    public static void MapBrowserAdminUsers(this RouteGroupBuilder group) {
        group.MapGet("/admin/users",async(HttpContext c,[AsParameters]BrowserAdminQuery q,IGetAdminUsersService s)=>{
            q.Validate(c); return BrowserApiResultMapper.From(c,await s.SearchAsync(new(BrowserAdminReportingEndpoints.Actor(c),q.SearchText,q.Role,q.IsActive,q.PageNumber??1,q.PageSize??25),c.RequestAborted));
        }).WithName("BrowserAdminUsers").Produces<PagedResponse<GetAdminUserResponse>>();
        group.MapPost("/admin/users",async(HttpContext c,ICreateInternalUserService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserAdminUserRequest>(c);
            return BrowserApiResultMapper.From(c,await s.CreateAsync(new(BrowserAdminReportingEndpoints.Actor(c),b.FullName,b.Email,b.PhoneNumber,b.Password,b.Role),c.RequestAborted));
        }).WithName("BrowserCreateInternalUser").Accepts<BrowserAdminUserRequest>("application/json").Produces<CreateInternalUserResponse>();
        group.MapPut("/admin/users/{id:guid}/active",async(HttpContext c,Guid id,ISetUserActiveStatusService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserAdminActiveRequest>(c); if(b.IsActive is null)throw new JsonException();
            return BrowserOperationsAssignmentEndpoints.Empty(c,await s.SetAsync(new(BrowserAdminReportingEndpoints.Actor(c),id,b.IsActive.Value,b.Reason),c.RequestAborted));
        }).WithName("BrowserSetUserActive").Accepts<BrowserAdminActiveRequest>("application/json").Produces(204);
        group.MapPut("/admin/users/{id:guid}/password",async(HttpContext c,Guid id,IResetUserPasswordService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserAdminPasswordRequest>(c);
            return BrowserOperationsAssignmentEndpoints.Empty(c,await s.ResetAsync(new(BrowserAdminReportingEndpoints.Actor(c),id,b.NewPassword,b.Reason),c.RequestAborted));
        }).WithName("BrowserResetUserPassword").Accepts<BrowserAdminPasswordRequest>("application/json").Produces(204);
        group.MapPut("/admin/users/{id:guid}/capacity",async(HttpContext c,Guid id,ISetShipperCapacityService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserAdminCapacityRequest>(c); if(b.IsAvailableForAssignment is null)throw new JsonException();
            return BrowserOperationsAssignmentEndpoints.Empty(c,await s.SetAsync(new(BrowserAdminReportingEndpoints.Actor(c),id,b.IsAvailableForAssignment.Value,b.MaxActiveShipments),c.RequestAborted));
        }).WithName("BrowserSetShipperCapacity").Accepts<BrowserAdminCapacityRequest>("application/json").Produces(204);
        group.MapPut("/admin/users/{id:guid}/working-areas",async(HttpContext c,Guid id,ISetShipperWorkingAreasService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserAdminAreasRequest>(c); if(b.Areas is null||b.Areas.Count>200)throw new JsonException();
            return BrowserApiResultMapper.From(c,await s.SetAsync(new(BrowserAdminReportingEndpoints.Actor(c),id,b.Areas),c.RequestAborted));
        }).WithName("BrowserSetShipperAreas").Accepts<BrowserAdminAreasRequest>("application/json").Produces<IReadOnlyList<ShipperWorkingAreaResponse>>();
    }
}
