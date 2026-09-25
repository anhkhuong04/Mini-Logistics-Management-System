using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints;

public static class PartnerApiEndpoints
{
    private const string AuthenticatedClientContextKey = "PartnerApi.AuthenticatedClient";
    private const string DetailedRequestAuditWrittenKey = "PartnerApi.DetailedRequestAuditWritten";
    private static readonly JsonSerializerOptions RequestHashJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapPartnerApiEndpoints(
        this IEndpointRouteBuilder endpoints,
        string corsPolicyName,
        string ingressRateLimitPolicyName)
    {
        var group = endpoints
            .MapGroup("/api/v1/partner")
            .WithGroupName("v1")
            .WithTags("Partner API")
            .RequireCors(corsPolicyName)
            .RequireRateLimiting(ingressRateLimitPolicyName)
            .AddEndpointFilter(AuditPartnerRequestAsync);

        group.MapPost("/shipping/quote", QuoteAsync)
            .WithName("PartnerQuote")
            .WithSummary("Calculate a shipping quote")
            .Produces<PartnerShippingQuoteResponse>()
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status429TooManyRequests);
        group.MapPost("/shipments", CreateShipmentAsync)
            .WithName("PartnerCreateShipment")
            .WithSummary("Create a shipment with an idempotency key")
            .Produces<PartnerShipmentResponse>(StatusCodes.Status201Created)
            .Produces<PartnerShipmentResponse>(StatusCodes.Status200OK)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status429TooManyRequests)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/shipments/{trackingCode}", GetShipmentAsync)
            .WithName("PartnerTrackShipment")
            .WithSummary("Track any shipment owned by the API client's shop")
            .Produces<PartnerShipmentTrackingResponse>()
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status429TooManyRequests);
        group.MapPost("/shipments/{trackingCode}/cancel", CancelShipmentAsync)
            .WithName("PartnerCancelShipment")
            .WithSummary("Cancel a shipment created by the same API client")
            .Produces<PartnerShipmentTrackingResponse>()
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status429TooManyRequests)
            .Produces<PartnerApiErrorResponse>(StatusCodes.Status503ServiceUnavailable);

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

        if (!HasScope(authenticationResult.Value, PartnerApiScope.Quote))
        {
            return ToErrorResult(PartnerApiErrors.MissingScope, httpContext);
        }

        var rateLimitError = await AcquireRateLimitAsync(
            httpContext,
            rateLimiter,
            authenticationResult.Value.ApiClientId,
            PartnerApiRateLimitKind.Quote);
        if (rateLimitError is not null)
        {
            return ToErrorResult(rateLimitError, httpContext);
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
        IPartnerApiRequestAuditRepository auditRepository,
        TimeProvider timeProvider)
    {
        var startedAtUtc = timeProvider.GetUtcNow();
        var authenticationResult = await AuthenticateAsync(httpContext, authenticationService);
        if (authenticationResult.IsFailure)
        {
            return ToErrorResult(authenticationResult.Error, httpContext);
        }

        if (!HasScope(authenticationResult.Value, PartnerApiScope.CreateShipment))
        {
            return ToErrorResult(PartnerApiErrors.MissingScope, httpContext);
        }

        var rateLimitError = await AcquireRateLimitAsync(
            httpContext,
            rateLimiter,
            authenticationResult.Value.ApiClientId,
            PartnerApiRateLimitKind.CreateShipment);
        if (rateLimitError is not null)
        {
            return ToErrorResult(rateLimitError, httpContext);
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
                auditRepository,
                timeProvider);

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
                auditRepository,
                timeProvider);

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
            auditRepository,
            timeProvider);

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

        if (!HasScope(authenticationResult.Value, PartnerApiScope.TrackShipment))
        {
            return ToErrorResult(PartnerApiErrors.MissingScope, httpContext);
        }

        var rateLimitError = await AcquireRateLimitAsync(
            httpContext,
            rateLimiter,
            authenticationResult.Value.ApiClientId,
            PartnerApiRateLimitKind.Tracking);
        if (rateLimitError is not null)
        {
            return ToErrorResult(rateLimitError, httpContext);
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

        if (!HasScope(authenticationResult.Value, PartnerApiScope.CancelShipment))
        {
            return ToErrorResult(PartnerApiErrors.MissingScope, httpContext);
        }

        var rateLimitError = await AcquireRateLimitAsync(
            httpContext,
            rateLimiter,
            authenticationResult.Value.ApiClientId,
            PartnerApiRateLimitKind.CancelShipment);
        if (rateLimitError is not null)
        {
            return ToErrorResult(rateLimitError, httpContext);
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
        return AuthenticateAndStoreContextAsync(httpContext, authenticationService);
    }

    private static async Task<Result<PartnerApiClientContext>> AuthenticateAndStoreContextAsync(
        HttpContext httpContext,
        IPartnerApiAuthenticationService authenticationService)
    {
        var result = await authenticationService.AuthenticateAsync(
            httpContext.Request.Headers.Authorization.ToString(),
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.RequestAborted);
        if (result.IsSuccess)
        {
            httpContext.Items[AuthenticatedClientContextKey] = result.Value;
        }

        return result;
    }

    private static async ValueTask<object?> AuditPartnerRequestAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        var httpContext = invocationContext.HttpContext;
        var timeProvider = httpContext.RequestServices.GetRequiredService<TimeProvider>();
        var startedAtUtc = timeProvider.GetUtcNow();
        var result = await next(invocationContext);
        if (httpContext.Items.ContainsKey(DetailedRequestAuditWrittenKey)
            || !httpContext.Items.TryGetValue(AuthenticatedClientContextKey, out var contextValue)
            || contextValue is not PartnerApiClientContext clientContext)
        {
            return result;
        }

        try
        {
            var statusCode = result is IStatusCodeHttpResult statusResult
                ? statusResult.StatusCode ?? StatusCodes.Status200OK
                : StatusCodes.Status200OK;
            var completedAtUtc = timeProvider.GetUtcNow();
            var repository = httpContext.RequestServices.GetRequiredService<IPartnerApiRequestAuditRepository>();
            await repository.AddAsync(
                new PartnerApiRequestAudit(
                    clientContext.ApiClientId,
                    clientContext.ShopId,
                    httpContext.Request.Method,
                    httpContext.Request.Path.Value ?? "/api/v1/partner",
                    httpContext.TraceIdentifier,
                    externalOrderId: null,
                    idempotencyKey: null,
                    ComputeRequestHash(new { httpContext.Request.Method, Path = httpContext.Request.Path.Value }),
                    statusCode,
                    CalculateDurationMs(startedAtUtc, completedAtUtc),
                    statusCode is >= 200 and < 400,
                    isIdempotentReplay: false,
                    shipmentId: null,
                    trackingCode: null,
                    errorCode: statusCode >= 400 ? $"HTTP.{statusCode}" : null,
                    errorMessage: null,
                    completedAtUtc),
                httpContext.RequestAborted);
            await repository.SaveChangesAsync(httpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(PartnerApiEndpoints));
            logger.LogDebug("Partner API request audit was cancelled for {Path}.", httpContext.Request.Path);
        }
        catch (Exception exception)
        {
            var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(PartnerApiEndpoints));
            logger.LogError(exception, "Failed to persist partner API request audit for {Path}.", httpContext.Request.Path);
        }

        return result;
    }

    private static bool HasScope(PartnerApiClientContext context, PartnerApiScope requiredScope)
    {
        return (context.Scopes & requiredScope) == requiredScope;
    }

    private static async ValueTask<Error?> AcquireRateLimitAsync(
        HttpContext httpContext,
        IPartnerApiRateLimiter rateLimiter,
        Guid apiClientId,
        PartnerApiRateLimitKind kind)
    {
        var decision = await rateLimiter.AcquireAsync(
            apiClientId,
            kind,
            httpContext.RequestAborted);
        if (decision.IsAllowed)
        {
            return null;
        }

        httpContext.Response.Headers.RetryAfter = Math.Ceiling(decision.RetryAfter.TotalSeconds).ToString("0");
        return decision.StoreUnavailable
            ? PartnerApiErrors.RateLimitUnavailable
            : PartnerApiErrors.RateLimitExceeded;
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
        IPartnerApiRequestAuditRepository auditRepository,
        TimeProvider timeProvider)
    {
        try
        {
            var completedAtUtc = timeProvider.GetUtcNow();
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
                CalculateDurationMs(startedAtUtc, completedAtUtc),
                isSuccess,
                isIdempotentReplay,
                shipment?.ShipmentId,
                shipment?.TrackingCode,
                error?.Code,
                error?.Description,
                completedAtUtc);

            await auditRepository.AddAsync(audit, httpContext.RequestAborted);
            await auditRepository.SaveChangesAsync(httpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(PartnerApiEndpoints));
            logger.LogDebug(
                "Partner API request audit was cancelled for {Path}. TraceId: {TraceId}",
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        catch (Exception exception)
        {
            var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(PartnerApiEndpoints));
            logger.LogError(
                exception,
                "Failed to persist detailed partner API request audit for {Path}. TraceId: {TraceId}",
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        finally
        {
            // Audit telemetry is non-critical and must never change the outcome of
            // an already completed shipment operation.
            httpContext.Items[DetailedRequestAuditWrittenKey] = true;
        }
    }

    private static int CalculateDurationMs(DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc)
    {
        var elapsed = completedAtUtc - startedAtUtc;
        return (int)Math.Clamp(elapsed.TotalMilliseconds, 0, int.MaxValue);
    }

    private static string ComputeRequestHash(object? request)
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
            "PartnerApi.ApiClientInactive" or "PartnerApi.ApiClientExpired" or "PartnerApi.IpNotAllowed"
                or "PartnerApi.MissingScope" or "PartnerApi.ShopInactive" or "Application.Forbidden" => StatusCodes.Status403Forbidden,
            "PartnerApi.RateLimitExceeded" => StatusCodes.Status429TooManyRequests,
            "PartnerApi.RateLimitUnavailable" => StatusCodes.Status503ServiceUnavailable,
            "Application.NotFound" => StatusCodes.Status404NotFound,
            "Application.Conflict" or "Application.ConcurrencyConflict" or "PartnerApi.IdempotencyConflict" => StatusCodes.Status409Conflict,
            "Application.ValidationFailed" => StatusCodes.Status400BadRequest,
            var code when code.StartsWith("Shipment.", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
    }

}
