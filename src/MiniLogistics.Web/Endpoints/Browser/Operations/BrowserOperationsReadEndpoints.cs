using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Operations;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed class BrowserOperationsQuery : BrowserDateRangeQuery
{
    [FromQuery]
    public string? SearchText
    {
        get; init;
    }
    [FromQuery]
    public string? Province
    {
        get; init;
    }
    [FromQuery]
    public string? Status
    {
        get; init;
    }
    [FromQuery]
    public string? CodStatus
    {
        get; init;
    }
    [FromQuery]
    public Guid? ShipperId
    {
        get; init;
    }
    [FromQuery]
    public string? MinCodAmount
    {
        get; init;
    }
    [FromQuery]
    public string? MaxCodAmount
    {
        get; init;
    }
    [FromQuery]
    public bool? SlaOnly
    {
        get; init;
    }
    [FromQuery]
    public int? PageNumber
    {
        get; init;
    }
    [FromQuery]
    public int? PageSize
    {
        get; init;
    }
    public GetOperationsShipmentsQuery ToQuery(HttpContext context)
    {
        var dates = Dates();
        var min = Parse<decimal>(MinCodAmount);
        var max = Parse<decimal>(MaxCodAmount);
        var page = PageNumber ?? 1;
        var size = PageSize ?? 25;
        if (context.Request.Query.Any(pair => pair.Value.Count != 1) || page < 1 || size is < 1 or > 100
            || (long)(page - 1) * size > int.MaxValue || min < 0 || max < 0 || min > max
            || SearchText?.Length > 100 || Province?.Length > 100 || ShipperId == Guid.Empty)
            throw new JsonException();
        return new(SearchText?.Trim(), Parse<ShipmentStatus>(Status), Parse<CodStatus>(CodStatus), ShipperId, Province?.Trim(),
            dates.From, dates.To, min, max, SlaOnly ?? false, page, size);
    }
}
public static class BrowserOperationsReadEndpoints
{
    public static void MapBrowserOperationsReads(this RouteGroupBuilder group)
    {
        group.MapGet("/operations/shipments", async (HttpContext context, [AsParameters] BrowserOperationsQuery query, IOperationsReadService service) =>
            BrowserApiResultMapper.From(context, await service.ShipmentsAsync(BrowserUserClaims.GetUserId(context), query.ToQuery(context), context.RequestAborted)))
            .WithName("BrowserOperationsShipments").Produces<PagedResponse<GetOperationsShipmentResponse>>();
        group.MapGet("/operations/pending-pickups", async (HttpContext context, [AsParameters] BrowserOperationsQuery query, IOperationsReadService service) =>
        {
            var q = query.ToQuery(context);
            return BrowserApiResultMapper.From(context, await service.PendingAsync(BrowserUserClaims.GetUserId(context), new GetPendingPickupShipmentsQuery(
                q.SearchText, q.Province, q.FromUtc, q.ToUtc, q.MinCodAmount, q.MaxCodAmount, q.SlaOnly, q.PageNumber, q.PageSize), context.RequestAborted));
        }).WithName("BrowserOperationsPending").Produces<PagedResponse<GetPendingPickupShipmentResponse>>();
        group.MapGet("/operations/shippers", async (HttpContext context, IOperationsReadService service) =>
            BrowserApiResultMapper.From(context, await service.ShippersAsync(BrowserUserClaims.GetUserId(context), context.RequestAborted)))
            .WithName("BrowserOperationsShippers").Produces<IReadOnlyList<OperationsShipperResponse>>();
        group.MapGet("/operations/cod/settlement-candidates", async (HttpContext context, [AsParameters] BrowserOperationsQuery query, IOperationsReadService service) =>
        {
            var q = query.ToQuery(context);
            return BrowserApiResultMapper.From(context, await service.SettlementAsync(BrowserUserClaims.GetUserId(context), q.PageNumber, q.PageSize, context.RequestAborted));
        }).WithName("BrowserOperationsSettlementCandidates").Produces<PagedResponse<OperationsCodCandidateResponse>>();
    }
}
