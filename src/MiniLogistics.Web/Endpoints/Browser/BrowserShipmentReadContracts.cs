using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Web.Endpoints.Browser;

public class BrowserDateRangeQuery
{
    [FromQuery(Name = "fromUtc")] public string? FromUtc { get; init; }
    [FromQuery(Name = "toUtc")] public string? ToUtc { get; init; }

    public (DateTimeOffset? From, DateTimeOffset? To) Dates()
    {
        var from = Parse<DateTimeOffset>(FromUtc); var to = Parse<DateTimeOffset>(ToUtc);
        if (from > to) throw new JsonException();
        return (from, to);
    }
    protected static T? Parse<T>(string? text) where T : struct => string.IsNullOrWhiteSpace(text) ? null
        : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(text), BrowserApiJsonOptions.Value);
}

public sealed class BrowserShipmentReadQuery : BrowserDateRangeQuery
{
    [FromQuery(Name = "status")] public string? Status { get; init; }
    [FromQuery(Name = "trackingCode")] public string? TrackingCode { get; init; }
    [FromQuery(Name = "receiverName")] public string? ReceiverName { get; init; }
    [FromQuery(Name = "receiverPhone")] public string? ReceiverPhone { get; init; }
    [FromQuery(Name = "minCodAmount")] public string? MinCodAmount { get; init; }
    [FromQuery(Name = "maxCodAmount")] public string? MaxCodAmount { get; init; }
    [FromQuery(Name = "sortBy")] public string? SortBy { get; init; }
    [FromQuery(Name = "sortDirection")] public string? SortDirection { get; init; }
    [FromQuery(Name = "pageNumber")] public int? PageNumber { get; init; }
    [FromQuery(Name = "pageSize")] public int? PageSize { get; init; }

    public GetShipmentsForCurrentShopQuery ToQuery(Guid actor, Guid shop)
    {
        var dates = Dates(); var min = Parse<decimal>(MinCodAmount); var max = Parse<decimal>(MaxCodAmount);
        var page = PageNumber ?? 1; var size = PageSize ?? 25;
        if (page < 1 || size is < 1 or > 100 || min < 0 || max < 0 || min > max
            || TrackingCode?.Length > 100 || ReceiverName?.Length > 100 || ReceiverPhone?.Length > 100) throw new JsonException();
        return new GetShipmentsForCurrentShopQuery(actor, shop, Parse<ShipmentStatus>(Status), TrackingCode?.Trim(),
            ReceiverName?.Trim(), ReceiverPhone?.Trim(), dates.From, dates.To, min, max,
            Parse<ShopShipmentSortBy>(SortBy) ?? ShopShipmentSortBy.CreatedAt,
            Parse<SortDirection>(SortDirection) ?? MiniLogistics.Application.Shipments.SortDirection.Descending, page, size);
    }
}
