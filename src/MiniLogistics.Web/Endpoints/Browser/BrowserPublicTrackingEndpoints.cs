using System.Globalization;
using MiniLogistics.Application.Shipments.GetPublicTracking;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserPublicTrackingEndpoints
{
    public static void MapBrowserPublicTracking(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/api/v1/public/tracking/{trackingCode}", async (HttpContext context,
            string trackingCode, IGetPublicTrackingService service, IPublicTrackingRateLimiter limiter) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            // ForwardedHeaders middleware has already applied the host's trusted proxy policy.
            var address = context.Connection.RemoteIpAddress;
            if (address is null)
                return BrowserApiResultMapper.Problem(context, "Application.Unavailable", 503);

            var normalizedCode = trackingCode.Trim();
            var clientKey = (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
            if (!limiter.TryAcquire(clientKey, normalizedCode, out var retryAfter))
            {
                context.Response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
                return BrowserApiResultMapper.Problem(context, "Browser.RateLimitExceeded", 429);
            }

            var result = await service.GetAsync(normalizedCode, phoneLast4: null, context.RequestAborted);
            if (result.IsFailure) return BrowserApiResultMapper.Problem(context, result.Error.Code);
            var summary = result.Value;
            return BrowserApiResultMapper.Json(new BrowserPublicTrackingResponse(
                summary.TrackingCode, PublicTrackingAccessLevel.Summary, summary.CurrentStatus,
                summary.PickupProvince, summary.DeliveryProvince, summary.CreatedAtUtc, summary.LastUpdatedAtUtc,
                summary.SenderName, summary.ReceiverName, summary.Timeline, BrowserSessionReader.ServerTimeZoneId));
        }).AllowAnonymous().WithGroupName("browser-v1").WithName("BrowserPublicTracking")
        .Produces<BrowserPublicTrackingResponse>()
        .Produces<BrowserApiProblem>(400, "application/problem+json")
        .Produces<BrowserApiProblem>(404, "application/problem+json")
        .Produces<BrowserApiProblem>(429, "application/problem+json")
        .Produces<BrowserApiProblem>(503, "application/problem+json");
}
