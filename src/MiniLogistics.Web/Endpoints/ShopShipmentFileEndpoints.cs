using System.Security.Claims;
using MiniLogistics.Application.Shipments.ExportShopShipments;
using MiniLogistics.Application.Shipments.GenerateShipmentLabel;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Web.Endpoints;

public static class ShopShipmentFileEndpoints
{
    public static IEndpointRouteBuilder MapShopShipmentFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/shop/files")
            .RequireAuthorization(policy => policy.RequireRole("Shop"));

        group.MapGet("/shipments/export.csv", ExportShipmentsAsync);
        group.MapGet("/shipments/{shipmentId:guid}/label.pdf", GenerateLabelAsync);

        return endpoints;
    }

    private static async Task<IResult> ExportShipmentsAsync(
        HttpContext httpContext,
        IExportShopShipmentsCsvService exportService)
    {
        if (!TryGetCurrentUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        var query = httpContext.Request.Query;
        var result = await exportService.ExportAsync(
            new ExportShopShipmentsCsvCommand(
                userId,
                ParseGuid(query["shopId"].ToString()),
                ParseEnum<ShipmentStatus>(query["status"].ToString()),
                EmptyToNull(query["trackingCode"].ToString()),
                EmptyToNull(query["receiverName"].ToString()),
                EmptyToNull(query["receiverPhone"].ToString()),
                ParseDateTimeOffset(query["fromUtc"].ToString()),
                ParseDateTimeOffset(query["toUtc"].ToString()),
                ParseDecimal(query["minCodAmount"].ToString()),
                ParseDecimal(query["maxCodAmount"].ToString())),
            httpContext.RequestAborted);

        return result.IsSuccess
            ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : Results.BadRequest(result.Error.Description);
    }

    private static async Task<IResult> GenerateLabelAsync(
        HttpContext httpContext,
        Guid shipmentId,
        IGenerateShipmentLabelService labelService)
    {
        if (!TryGetCurrentUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await labelService.GenerateAsync(
            new GenerateShipmentLabelCommand(
                userId,
                shipmentId,
                ParseGuid(httpContext.Request.Query["shopId"].ToString())),
            httpContext.RequestAborted);

        return result.IsSuccess
            ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : Results.BadRequest(result.Error.Description);
    }

    private static bool TryGetCurrentUserId(HttpContext httpContext, out Guid userId)
    {
        return Guid.TryParse(
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }

    private static Guid? ParseGuid(string value)
    {
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static TEnum? ParseEnum<TEnum>(string value)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result)
            ? result
            : null;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string value)
    {
        return DateTimeOffset.TryParse(value, out var date) ? date : null;
    }

    private static decimal? ParseDecimal(string value)
    {
        return decimal.TryParse(value, out var amount) ? amount : null;
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
