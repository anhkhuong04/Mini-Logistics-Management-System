using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using MiniLogistics.Application.Shipments.ImportShipments;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserRequestBodyLimit(long Bytes);
public sealed record BrowserShipmentImportConfirmRequest(IReadOnlyList<ShipmentImportRowDraft> Rows);

public static class BrowserShipmentImportEndpoints
{
    public const long FileLimit = 2_000_000;
    public const long RequestLimit = 3_000_000;
    public static void MapBrowserShipmentImports(this RouteGroupBuilder group)
    {
        group.MapPost("/shops/{shopId:guid}/shipment-imports/preview", PreviewAsync)
            .WithName("BrowserPreviewShipmentImport").Accepts<IFormFile>("multipart/form-data")
            .WithMetadata(new BrowserRequestBodyLimit(RequestLimit)).Produces<ShipmentImportPreviewResponse>();
        group.MapPost("/shops/{shopId:guid}/shipment-imports/confirm", async (HttpContext context, Guid shopId,
            IConfirmShipmentImportService service, IShopUiActionRateLimiter limiter) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var request = await BrowserApiEndpoints.ReadAsync<BrowserShipmentImportConfirmRequest>(context);
            if (shopId == Guid.Empty || request.Rows is null || request.Rows.Count is < 1 or > 500
                || request.Rows.Any(row => row is null || row.RowNumber < 1)
                || request.Rows.Select(row => row.RowNumber).Distinct().Count() != request.Rows.Count) throw new JsonException();
            if (Limit(context, limiter, actor, ShopUiActionKind.ImportConfirm) is { } denied) return denied;
            return BrowserApiResultMapper.From(context, await service.ConfirmAsync(new(actor, shopId, request.Rows), context.RequestAborted));
        }).WithName("BrowserConfirmShipmentImport").Accepts<BrowserShipmentImportConfirmRequest>("application/json")
            .WithMetadata(new BrowserRequestBodyLimit(RequestLimit)).Produces<ShipmentImportConfirmResponse>();
        group.MapGet("/shops/{shopId:guid}/shipment-imports/{batchId:guid}", async (HttpContext context, Guid shopId, Guid batchId, IGetShipmentImportBatchService service) =>
        {
            if (shopId == Guid.Empty || batchId == Guid.Empty) throw new JsonException();
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            return BrowserApiResultMapper.From(context, await service.GetAsync(actor, shopId, batchId, context.RequestAborted));
        }).WithName("BrowserShipmentImportBatch").Produces<ShipmentImportConfirmResponse>();
    }
    private static async Task<IResult> PreviewAsync(HttpContext context, Guid shopId,
        IPreviewShipmentImportService service, IShopUiActionRateLimiter limiter)
    {
        if (shopId == Guid.Empty) throw new JsonException();
        if (!context.Request.HasFormContentType || !context.Request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return BrowserApiResultMapper.Problem(context, "Request.UnsupportedMediaType", 415);
        BrowserUserClaims.TryGetUserId(context.User, out var actor);
        if (Limit(context, limiter, actor, ShopUiActionKind.ImportPreview) is { } denied) return denied;
        IFormCollection form;
        try { form = await context.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = RequestLimit, ValueLengthLimit = 1024, MultipartHeadersLengthLimit = 8192 }, context.RequestAborted); }
        catch (InvalidDataException) { return BrowserApiResultMapper.Problem(context, "Request.InvalidMultipart", 400); }
        if (form.Files.Count != 1 || form.Files[0].Name != "file" || form.Count != 0) throw new JsonException();
        var file = form.Files[0];
        if (file.Length > FileLimit) return BrowserApiResultMapper.Problem(context, "Request.TooLarge", 413);
        if (file.Length == 0 || !Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)) throw new JsonException();
        var mime = file.ContentType.Split(';')[0].Trim().ToLowerInvariant();
        if (mime is not ("text/csv" or "text/plain" or "application/csv" or "application/vnd.ms-excel" or "application/octet-stream" or ""))
            return BrowserApiResultMapper.Problem(context, "Request.UnsupportedMediaType", 415);
        using var stream = file.OpenReadStream(); using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, context.RequestAborted);
        string csv;
        try { csv = new UTF8Encoding(false, true).GetString(buffer.ToArray()).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw new JsonException(); }
        if (csv.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t'))) throw new JsonException();
        return BrowserApiResultMapper.From(context, await service.PreviewAsync(new(actor, shopId, csv), context.RequestAborted));
    }
    private static IResult? Limit(HttpContext context, IShopUiActionRateLimiter limiter, Guid actor, ShopUiActionKind kind)
    {
        if (limiter.TryAcquire(actor, kind, out var retry)) return null;
        context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return BrowserApiResultMapper.Problem(context, "Browser.RateLimitExceeded");
    }
}
