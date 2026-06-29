using MiniLogistics.Application.Common;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Web.Endpoints;

public static class PartnerApiEndpoints
{
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
        IPartnerQuoteService quoteService)
    {
        var authenticationResult = await AuthenticateAsync(httpContext, authenticationService);
        if (authenticationResult.IsFailure)
        {
            return ToErrorResult(authenticationResult.Error, httpContext);
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
        IPartnerCreateShipmentService createShipmentService)
    {
        var authenticationResult = await AuthenticateAsync(httpContext, authenticationService);
        if (authenticationResult.IsFailure)
        {
            return ToErrorResult(authenticationResult.Error, httpContext);
        }

        if (request?.Receiver is null || request.DeliveryAddress is null || request.Parcel is null)
        {
            return ToErrorResult(
                ApplicationErrors.ValidationFailed("Receiver, delivery address and parcel are required."),
                httpContext);
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString().Trim();
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
            return ToErrorResult(createResult.Error, httpContext);
        }

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
        IPartnerShipmentQueryService shipmentQueryService)
    {
        var authenticationResult = await AuthenticateAsync(httpContext, authenticationService);
        if (authenticationResult.IsFailure)
        {
            return ToErrorResult(authenticationResult.Error, httpContext);
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
        IPartnerCancelShipmentService cancelShipmentService)
    {
        var authenticationResult = await AuthenticateAsync(httpContext, authenticationService);
        if (authenticationResult.IsFailure)
        {
            return ToErrorResult(authenticationResult.Error, httpContext);
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
            "PartnerApi.ApiClientInactive" or "Application.Forbidden" => StatusCodes.Status403Forbidden,
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
