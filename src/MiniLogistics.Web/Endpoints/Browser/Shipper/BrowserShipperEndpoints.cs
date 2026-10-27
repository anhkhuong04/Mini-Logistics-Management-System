using System.Globalization;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.CashOnDelivery.GetShipperCodDailySummary;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Application.Shippers.GetShipperWorkingAreas;
using MiniLogistics.Application.Shippers.SetShipperAvailability;
using MiniLogistics.Application.Shippers.Workspace;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed class BrowserShipperQuery
{
    [FromQuery(Name = "searchText")]
    public string? SearchText
    {
        get; init;
    }
    [FromQuery(Name = "stage")]
    public string? Stage
    {
        get; init;
    }
    [FromQuery(Name = "codStatus")]
    public string? CodStatus
    {
        get; init;
    }
    [FromQuery(Name = "pageNumber")]
    public int? PageNumber
    {
        get; init;
    }
    [FromQuery(Name = "pageSize")]
    public int? PageSize
    {
        get; init;
    }
    private static T? Parse<T>(string? value) where T : struct => string.IsNullOrWhiteSpace(value) ? null
        : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value), BrowserApiJsonOptions.Value);
    public GetAssignedShipmentsForShipperQuery ToQuery(HttpContext context)
    {
        BrowserApiEndpoints.RequireUniqueQuery(context);
        var page = PageNumber ?? 1;
        var size = PageSize ?? 20;
        if (SearchText?.Length > 100 || page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
            throw new JsonException();
        return new(SearchText?.Trim(), Parse<ShipperWorkspaceStage>(Stage), Parse<CodStatus>(CodStatus), page, size);
    }
}
public sealed record BrowserShipperAvailabilityRequest(bool? IsAvailableForAssignment);
public static class BrowserShipperEndpoints
{
    private const string Prefix = "/shipper/me";
    public static void MapBrowserShipper(this RouteGroupBuilder group)
    {
        group.MapGet(Prefix + "/workspace", async (HttpContext context, [AsParameters] BrowserShipperQuery query, IGetAssignedShipmentsForShipperService service) =>
            BrowserApiResultMapper.From(context, await service.SearchAsync(BrowserUserClaims.GetUserId(context), query.ToQuery(context), context.RequestAborted)))
            .WithName("BrowserShipperWorkspace").Produces<PagedResponse<GetAssignedShipmentForShipperResponse>>();
        group.MapGet(Prefix + "/profile", async (HttpContext context, IShipperSelfService service) =>
            BrowserApiResultMapper.From(context, await service.ProfileAsync(BrowserUserClaims.GetUserId(context), context.RequestAborted)))
            .WithName("BrowserShipperProfile").Produces<ShipperSelfProfileResponse>();
        group.MapGet(Prefix + "/working-areas", async (HttpContext context, IGetShipperWorkingAreasService service) =>
            BrowserApiResultMapper.From(context, await service.GetAsync(BrowserUserClaims.GetUserId(context), BrowserUserClaims.GetUserId(context), context.RequestAborted)))
            .WithName("BrowserShipperWorkingAreas").Produces<IReadOnlyList<ShipperWorkingAreaResponse>>();
        group.MapGet(Prefix + "/cod-summary", async (HttpContext context, IGetShipperCodDailySummaryService service, [FromQuery] string? businessDate) =>
        {
            BrowserApiEndpoints.RequireUniqueQuery(context);
            DateOnly? date = null;
            if (businessDate is not null)
            {
                if (!DateOnly.TryParseExact(businessDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    throw new JsonException();
                date = parsed;
            }
            return BrowserApiResultMapper.From(context, await service.GetAsync(BrowserUserClaims.GetUserId(context), date, context.RequestAborted));
        }).WithName("BrowserShipperCodSummary").Produces<ShipperCodDailySummaryResponse>();
        group.MapPut(Prefix + "/availability", async (HttpContext context, IValidator<SetShipperAvailabilityCommand> validator, ISetShipperAvailabilityService service) =>
        {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserShipperAvailabilityRequest>(context);
            if (body.IsAvailableForAssignment is null)
                throw new JsonException();
            var command = new SetShipperAvailabilityCommand(BrowserUserClaims.GetUserId(context), body.IsAvailableForAssignment.Value);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors)
                : BrowserApiResultMapper.Empty(context, await service.SetAsync(command, context.RequestAborted));
        }).WithName("BrowserShipperAvailability").Accepts<BrowserShipperAvailabilityRequest>("application/json").Produces(204);
        group.MapPost(Prefix + "/shipments/{id:guid}/status", async (HttpContext context, Guid id, IValidator<UpdateShipmentStatusCommand> validator, IShipperSelfService service) =>
        {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserOperationsStatusRequest>(context);
            var command = new UpdateShipmentStatusCommand(id, BrowserUserClaims.GetUserId(context), body.NewStatus, body.Note, body.FailureReasonCode, body.GpsCoordinate);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors)
                : BrowserApiResultMapper.Empty(context, await service.StatusAsync(command, context.RequestAborted));
        }).WithName("BrowserShipperStatus").Accepts<BrowserOperationsStatusRequest>("application/json").Produces(204);
        group.MapPost(Prefix + "/shipments/{id:guid}/cod/collect", async (HttpContext context, Guid id, IValidator<MarkCodCollectedCommand> validator, IShipperSelfService service) =>
        {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserCodCollectRequest>(context);
            var command = new MarkCodCollectedCommand(id, BrowserUserClaims.GetUserId(context), body.CollectedAmount, body.Note);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors)
                : BrowserApiResultMapper.Empty(context, await service.CollectAsync(command, context.RequestAborted));
        }).WithName("BrowserShipperCollect").Accepts<BrowserCodCollectRequest>("application/json").Produces(204);
    }
}
