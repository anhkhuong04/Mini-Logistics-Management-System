using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints;

public static class PartnerApiEndpoints
{
    private static readonly JsonSerializerOptions RequestHashJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapPartnerApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/partner");

        group.MapPost("/shipping/quote", QuoteAsync);
        group.MapPost("/shipments", CreateShipmentAsync);
        group.MapGet("/shipments/{trackingCode}", GetShipmentAsync);
        group.MapPost("/shipments/{trackingCode}/cancel", CancelShipmentAsync);

        return endpoints;
    }

    private static async Task<IResult> QuoteAsync(
        HttpContext httpContext,
        PartnerQuoteRequest? request,
        IPartnerApiAuthenticationService authenticationService,
        IPartnerQuoteService quoteService,
        IPartnerApiRateLimiter rateLimiter)
    {
        var authenticationResult = await AuthenticateAsync(httpContext, authenticationService);
        if (authenticationResult.IsFailure)
        {
            return ToErrorResult(authenticationResult.Error, httpContext);
        }

        if (!TryAcquireRateLimit(httpContext, rateLimiter, authenticationResult.Value.ApiClientId, PartnerApiRateLimitKind.Quote))
        {
            return ToErrorResult(PartnerApiErrors.RateLimitExceeded, httpContext);
        }

        if (request?.DeliveryAddress is null || request.Parcel is null)
        {
            return ToErrorResult(
                ApplicationErrors.ValidationFailed("Delivery address and parcel are required."),
                httpContext);
        }

        var command = new PartnerQuoteCommand(
            authenticationResult.Value.ApiClientId,
            authenticationResult.Value.ShopId,
            ToShipmentAddress(request.PickupAddress),
            ToShipmentAddress(request.DeliveryAddress)!,
            request.Parcel.WeightKg,
            request.Parcel.LengthCm,
            request.Parcel.WidthCm,
            request.Parcel.HeightCm,
            request.GoodsValueAmount,
            request.CodAmount,
            string.IsNullOrWhiteSpace(request.Currency) ? "VND" : request.Currency,
            request.ExternalOrderId);

        var quoteResult = await quoteService.QuoteAsync(command, httpContext.RequestAborted);

        return quoteResult.IsSuccess
            ? Results.Ok(quoteResult.Value)
            : ToErrorResult(quoteResult.Error, httpContext);
    }

    private static async Task<IResult> CreateShipmentAsync(
        HttpContext httpContext,
        PartnerCreateShipmentRequest? request,
        IPartnerApiAuthenticationService authenticationService,
        IPartnerCreateShipmentService createShipmentService,
        IPartnerApiRateLimiter rateLimiter,
        IPartnerApiRequestAuditRepository auditRepository)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var authenticationResult = await AuthenticateAsync(httpContext, authenticationService);
        if (authenticationResult.IsFailure)
        {
            return ToErrorResult(authenticationResult.Error, httpContext);
        }

        if (!TryAcquireRateLimit(httpContext, rateLimiter, authenticationResult.Value.ApiClientId, PartnerApiRateLimitKind.CreateShipment))
        {
            return ToErrorResult(PartnerApiErrors.RateLimitExceeded, httpContext);
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString().Trim();
        if (request?.Receiver is null || request.DeliveryAddress is null || request.Parcel is null)
        {
            var validationError = ApplicationErrors.ValidationFailed("Receiver, delivery address and parcel are required.");
            await AuditCreateShipmentAsync(
                httpContext,
                authenticationResult.Value,
                request,
                idempotencyKey,
                startedAtUtc,
                StatusCodes.Status400BadRequest,
                isSuccess: false,
                isIdempotentReplay: false,
                shipment: null,
                error: validationError,
                auditRepository);

            return ToErrorResult(
                validationError,
                httpContext);
        }

        var command = new PartnerCreateShipmentCommand(
            authenticationResult.Value.ApiClientId,
            authenticationResult.Value.ShopId,
            request.ExternalOrderId ?? string.Empty,
            idempotencyKey,
            request.Sender?.Name,
            request.Sender?.Phone,
            request.Receiver.Name ?? string.Empty,
            request.Receiver.Phone ?? string.Empty,
            ToShipmentAddress(request.PickupAddress),
            ToShipmentAddress(request.DeliveryAddress)!,
            request.Parcel.WeightKg,
            request.Parcel.LengthCm,
            request.Parcel.WidthCm,
            request.Parcel.HeightCm,
            request.GoodsValueAmount,
            request.CodAmount,
            string.IsNullOrWhiteSpace(request.Currency) ? "VND" : request.Currency,
            request.Note);

        var createResult = await createShipmentService.CreateAsync(command, httpContext.RequestAborted);

        if (createResult.IsFailure)
        {
            var statusCode = ToStatusCode(createResult.Error);
            await AuditCreateShipmentAsync(
                httpContext,
                authenticationResult.Value,
                request,
                idempotencyKey,
                startedAtUtc,
                statusCode,
                isSuccess: false,
                isIdempotentReplay: false,
                shipment: null,
                error: createResult.Error,
                auditRepository);

            return ToErrorResult(createResult.Error, httpContext);
        }

        var successStatusCode = createResult.Value.IsIdempotentReplay
            ? StatusCodes.Status200OK
            : StatusCodes.Status201Created;
        await AuditCreateShipmentAsync(
            httpContext,
            authenticationResult.Value,
            request,
            idempotencyKey,
            startedAtUtc,
            successStatusCode,
            isSuccess: true,
            isIdempotentReplay: createResult.Value.IsIdempotentReplay,
            shipment: createResult.Value.Shipment,
            error: null,
            auditRepository);

        return createResult.Value.IsIdempotentReplay
            ? Results.Ok(createResult.Value.Shipment)
            : Results.Created(
                $"/api/v1/partner/shipments/{createResult.Value.Shipment.TrackingCode}",
                createResult.Value.Shipment);
    }

    private static async Task<IResult> GetShipmentAsync(
        HttpContext httpContext,
        string trackingCode,
        IPartnerApiAuthenticationService authenticationService,
        IPartnerShipmentQueryService shipmentQueryService,
        IPartnerApiRateLimiter rateLimiter)
    {
        var authenticationResult = await AuthenticateAsync(httpContext, authenticationService);
        if (authenticationResult.IsFailure)
        {
            return ToErrorResult(authenticationResult.Error, httpContext);
        }

        if (!TryAcquireRateLimit(httpContext, rateLimiter, authenticationResult.Value.ApiClientId, PartnerApiRateLimitKind.Tracking))
        {
            return ToErrorResult(PartnerApiErrors.RateLimitExceeded, httpContext);
        }

        var queryResult = await shipmentQueryService.GetAsync(new PartnerGetShipmentCommand(
            authenticationResult.Value.ApiClientId,
            authenticationResult.Value.ShopId,
            trackingCode),
            httpContext.RequestAborted);

        return queryResult.IsSuccess
            ? Results.Ok(queryResult.Value)
            : ToErrorResult(queryResult.Error, httpContext);
    }

    private static async Task<IResult> CancelShipmentAsync(
        HttpContext httpContext,
        string trackingCode,
        PartnerCancelShipmentRequest? request,
        IPartnerApiAuthenticationService authenticationService,
        IPartnerCancelShipmentService cancelShipmentService,
        IPartnerApiRateLimiter rateLimiter)
    {
        var authenticationResult = await AuthenticateAsync(httpContext, authenticationService);
        if (authenticationResult.IsFailure)
        {
            return ToErrorResult(authenticationResult.Error, httpContext);
        }

        if (!TryAcquireRateLimit(httpContext, rateLimiter, authenticationResult.Value.ApiClientId, PartnerApiRateLimitKind.CancelShipment))
        {
            return ToErrorResult(PartnerApiErrors.RateLimitExceeded, httpContext);
        }

        var cancelResult = await cancelShipmentService.CancelAsync(new PartnerCancelShipmentCommand(
            authenticationResult.Value.ApiClientId,
            authenticationResult.Value.ShopId,
            trackingCode,
            request?.Reason ?? string.Empty),
            httpContext.RequestAborted);

        return cancelResult.IsSuccess
            ? Results.Ok(cancelResult.Value)
            : ToErrorResult(cancelResult.Error, httpContext);
    }

    private static Task<Result<PartnerApiClientContext>> AuthenticateAsync(
        HttpContext httpContext,
        IPartnerApiAuthenticationService authenticationService)
    {
        return authenticationService.AuthenticateAsync(
            httpContext.Request.Headers.Authorization.ToString(),
            httpContext.RequestAborted);
    }

    private static bool TryAcquireRateLimit(
        HttpContext httpContext,
        IPartnerApiRateLimiter rateLimiter,
        Guid apiClientId,
        PartnerApiRateLimitKind kind)
    {
        var isAllowed = rateLimiter.TryAcquire(apiClientId, kind, out var retryAfter);
        if (!isAllowed)
        {
            httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
        }

        return isAllowed;
    }

    private static async Task AuditCreateShipmentAsync(
        HttpContext httpContext,
        PartnerApiClientContext context,
        PartnerCreateShipmentRequest? request,
        string idempotencyKey,
        DateTimeOffset startedAtUtc,
        int statusCode,
        bool isSuccess,
        bool isIdempotentReplay,
        PartnerShipmentResponse? shipment,
        Error? error,
        IPartnerApiRequestAuditRepository auditRepository)
    {
        var audit = new PartnerApiRequestAudit(
            context.ApiClientId,
            context.ShopId,
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? "/api/v1/partner/shipments",
            httpContext.TraceIdentifier,
            request?.ExternalOrderId,
            idempotencyKey,
            ComputeRequestHash(request),
            statusCode,
            CalculateDurationMs(startedAtUtc),
            isSuccess,
            isIdempotentReplay,
            shipment?.ShipmentId,
            shipment?.TrackingCode,
            error?.Code,
            error?.Description);

        await auditRepository.AddAsync(audit, httpContext.RequestAborted);
        await auditRepository.SaveChangesAsync(httpContext.RequestAborted);
    }

    private static int CalculateDurationMs(DateTimeOffset startedAtUtc)
    {
        var elapsed = DateTimeOffset.UtcNow - startedAtUtc;
        return (int)Math.Clamp(elapsed.TotalMilliseconds, 0, int.MaxValue);
    }

    private static string ComputeRequestHash(PartnerCreateShipmentRequest? request)
    {
        var json = request is null
            ? "{}"
            : JsonSerializer.Serialize(request, RequestHashJsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }

    private static ShipmentAddressDto? ToShipmentAddress(PartnerAddressRequest? address)
    {
        return address is null
            ? null
            : new ShipmentAddressDto(
                address.Street ?? string.Empty,
                address.Ward ?? string.Empty,
                address.Province ?? string.Empty,
                string.IsNullOrWhiteSpace(address.Country) ? "Vietnam" : address.Country);
    }

    private static IResult ToErrorResult(Error error, HttpContext httpContext)
    {
        var statusCode = ToStatusCode(error);
        return Results.Json(
            new PartnerApiErrorResponse(new PartnerApiError(error.Code, error.Description, httpContext.TraceIdentifier)),
            statusCode: statusCode);
    }

    private static int ToStatusCode(Error error)
    {
        return error.Code switch
        {
            "PartnerApi.MissingApiKey" or "PartnerApi.InvalidApiKey" => StatusCodes.Status401Unauthorized,
            "PartnerApi.ApiClientInactive" or "PartnerApi.ShopInactive" or "Application.Forbidden" => StatusCodes.Status403Forbidden,
            "PartnerApi.RateLimitExceeded" => StatusCodes.Status429TooManyRequests,
            "Application.NotFound" => StatusCodes.Status404NotFound,
            "Application.Conflict" or "PartnerApi.IdempotencyConflict" => StatusCodes.Status409Conflict,
            "Application.ValidationFailed" => StatusCodes.Status400BadRequest,
            var code when code.StartsWith("Shipment.", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
    }

    public sealed record PartnerQuoteRequest(
        string? ExternalOrderId,
        PartnerAddressRequest? PickupAddress,
        PartnerAddressRequest? DeliveryAddress,
        PartnerParcelRequest? Parcel,
        decimal GoodsValueAmount,
        decimal CodAmount,
        string? Currency);

    public sealed record PartnerCreateShipmentRequest(
        string? ExternalOrderId,
        PartnerPartyRequest? Sender,
        PartnerPartyRequest? Receiver,
        PartnerAddressRequest? PickupAddress,
        PartnerAddressRequest? DeliveryAddress,
        PartnerParcelRequest? Parcel,
        decimal GoodsValueAmount,
        decimal CodAmount,
        string? Currency,
        string? Note);

    public sealed record PartnerCancelShipmentRequest(
        string? Reason);

    public sealed record PartnerPartyRequest(
        string? Name,
        string? Phone);

    public sealed record PartnerAddressRequest(
        string? Street,
        string? Ward,
        string? Province,
        string? Country);

    public sealed record PartnerParcelRequest(
        decimal WeightKg,
        decimal LengthCm,
        decimal WidthCm,
        decimal HeightCm);

    public sealed record PartnerApiErrorResponse(PartnerApiError Error);

    public sealed record PartnerApiError(
        string Code,
        string Message,
        string TraceId);
}
