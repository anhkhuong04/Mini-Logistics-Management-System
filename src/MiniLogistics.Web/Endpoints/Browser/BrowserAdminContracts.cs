using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Web.Endpoints.Browser;
public sealed class BrowserAdminQuery : BrowserDateRangeQuery
{
    [FromQuery(Name="province")] public string? Province { get; init; }
    [FromQuery(Name="status")] public string? Status { get; init; }
    [FromQuery(Name="shipperId")] public Guid? ShipperId { get; init; }
    [FromQuery(Name="minAmount")] public string? MinAmount { get; init; }
    [FromQuery(Name="maxAmount")] public string? MaxAmount { get; init; }
    [FromQuery(Name="actorUserId")] public Guid? ActorUserId { get; init; }
    [FromQuery(Name="action")] public string? Action { get; init; }
    [FromQuery(Name="targetType")] public string? TargetType { get; init; }
    [FromQuery(Name="searchText")] public string? SearchText { get; init; }
    [FromQuery(Name="role")] public string? Role { get; init; }
    [FromQuery(Name="isActive")] public bool? IsActive { get; init; }
    [FromQuery(Name="isRegionalSortingHub")] public bool? IsRegionalSortingHub { get; init; }
    [FromQuery(Name="pageNumber")] public int? PageNumber { get; init; }
    [FromQuery(Name="pageSize")] public int? PageSize { get; init; }
    [FromQuery(Name="limit")] public int? Limit { get; init; }
    public T? Value<T>(string? value) where T:struct => Parse<T>(value);
    public void Validate(HttpContext context) {
        BrowserShipperEndpoints.UniqueQuery(context); Dates(); var page=PageNumber??1; var size=PageSize??25;
        if (page<1 || size is <1 or >100 || (long)(page-1)*size>int.MaxValue || Limit is <1 or >500
            || Province?.Length>100 || SearchText?.Length>100 || Role?.Length>50 || Action?.Length>120 || TargetType?.Length>80) throw new JsonException();
    }
}
public sealed record BrowserAdminActiveRequest(bool? IsActive, string? Reason = null);
public sealed record BrowserAdminUserRequest(string FullName, string Email, string PhoneNumber, string Password, string Role);
public sealed record BrowserAdminPasswordRequest(string NewPassword, string? Reason = null);
public sealed record BrowserAdminCapacityRequest(bool? IsAvailableForAssignment, int MaxActiveShipments);
public sealed record BrowserAdminHubRequest(string Code,string Name,string Province,string? Ward,string? AddressLine,string Country,bool IsRegionalSortingHub);
public sealed record BrowserAdminRouteRequest(string Province,string Region,string? Reason=null);
public sealed record BrowserAdminFeeRequest(RouteType RouteType,decimal BaseWeightKg,decimal BaseFeeAmount,decimal ExtraWeightStepKg,decimal ExtraStepFeeAmount,
    decimal? MinimumWeightKg,decimal? MaximumWeightKg,decimal InsuranceFreeThreshold,decimal InsuranceMaximumValue,decimal InsuranceRate,decimal ReturnFeeRate,string? Reason=null);
