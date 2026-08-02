using System.Security.Claims;
using MiniLogistics.Application.Shops.Reports;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.ExportShopShipments;
using MiniLogistics.Application.Shipments.GenerateShipmentLabel;
using MiniLogistics.Application.Shipments.ImportShipments;
using System.Text;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints;

public static class ShopShipmentFileEndpoints
{
    public static IEndpointRouteBuilder MapShopShipmentFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/shop/files")
            .RequireAuthorization(policy => policy.RequireRole("Shop"));

        group.MapGet("/shipments/export.csv", ExportShipmentsAsync);
        group.MapGet("/cod-report.csv", ExportCodReportAsync);
        group.MapGet("/shipments/{shipmentId:guid}/label.pdf", GenerateLabelAsync);
        group.MapGet("/import-batches/{batchId:guid}/errors.csv", ExportImportErrorsAsync);

        return endpoints;
    }

    private static async Task<IResult> ExportShipmentsAsync(
        HttpContext httpContext,
        IExportShopShipmentsCsvService exportService,
        IShopUiActionRateLimiter rateLimiter)
    {
        if (!TryGetCurrentUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        if (!rateLimiter.TryAcquire(userId, ShopUiActionKind.ExportShipments, out var retryAfter))
        {
            return RateLimitExceeded(httpContext, retryAfter);
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
                ParseDecimal(query["maxCodAmount"].ToString()),
                ParseEnum<ShopShipmentSortBy>(query["sortBy"].ToString()) ?? ShopShipmentSortBy.CreatedAt,
                ParseEnum<SortDirection>(query["sortDirection"].ToString()) ?? SortDirection.Descending),
            httpContext.RequestAborted);

        return result.IsSuccess
            ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : Results.BadRequest(result.Error.Description);
    }

    private static async Task<IResult> ExportCodReportAsync(
        HttpContext httpContext,
        IExportShopCodReportCsvService exportService,
        IShopUiActionRateLimiter rateLimiter)
    {
        if (!TryGetCurrentUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        if (!rateLimiter.TryAcquire(userId, ShopUiActionKind.ExportCodReport, out var retryAfter))
        {
            return RateLimitExceeded(httpContext, retryAfter);
        }

        var query = httpContext.Request.Query;
        var result = await exportService.ExportAsync(
            new ExportShopCodReportCsvCommand(
                userId,
                ParseGuid(query["shopId"].ToString()),
                ParseDateTimeOffset(query["fromUtc"].ToString()),
                ParseDateTimeOffset(query["toUtc"].ToString())),
            httpContext.RequestAborted);

        return result.IsSuccess
            ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : Results.BadRequest(result.Error.Description);
    }

    private static async Task<IResult> GenerateLabelAsync(
        HttpContext httpContext,
        Guid shipmentId,
        IGenerateShipmentLabelService labelService,
        IShopUiActionRateLimiter rateLimiter)
    {
        if (!TryGetCurrentUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        if (!rateLimiter.TryAcquire(userId, ShopUiActionKind.GenerateLabel, out var retryAfter))
        {
            return RateLimitExceeded(httpContext, retryAfter);
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

    private static async Task<IResult> ExportImportErrorsAsync(
        HttpContext httpContext,
        Guid batchId,
        IExportShipmentImportErrorsService exportService)
    {
        if (!TryGetCurrentUserId(httpContext, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await exportService.ExportCsvAsync(
            userId,
            ParseGuid(httpContext.Request.Query["shopId"].ToString()),
            batchId,
            httpContext.RequestAborted);
        return result.IsSuccess
            ? Results.File(
                Encoding.UTF8.GetBytes(result.Value),
                "text/csv; charset=utf-8",
                $"shipment-import-{batchId:N}-errors.csv")
            : Results.BadRequest(result.Error.Description);
    }

    private static bool TryGetCurrentUserId(HttpContext httpContext, out Guid userId)
    {
        return Guid.TryParse(
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }

    private static IResult RateLimitExceeded(HttpContext httpContext, TimeSpan retryAfter)
    {
        httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString();
        return Results.Problem(
            title: "Rate limit exceeded.",
            detail: "Too many Shop file actions. Please retry later.",
            statusCode: StatusCodes.Status429TooManyRequests);
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
