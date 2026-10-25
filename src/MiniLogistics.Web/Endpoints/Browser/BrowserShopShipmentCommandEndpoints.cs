using FluentValidation;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shipments.DraftShipments;
using MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserShopShipmentCommandEndpoints
{
    public static void MapBrowserShopShipmentCommands(this RouteGroupBuilder group)
    {
        group.MapPost("/shops/{shopId:guid}/shipments", async (HttpContext context, Guid shopId,
            IValidator<CreateShipmentCommand> validator, ICreateShipmentService service, IShopUiActionRateLimiter limiter) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var body = await BrowserApiEndpoints.ReadAsync<BrowserShipmentWriteRequest>(context);
            if (StorageProblem(context, body) is { } invalid) return invalid;
            var command = body.Create(actor, shopId);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            if (!validation.IsValid) return BrowserValidationProblem.From(context, validation.Errors);
            if (shopId == Guid.Empty) return BrowserApiResultMapper.Problem(context, "Application.ValidationFailed");
            if (Limit(context, limiter, actor) is { } denied) return denied;
            var result = await service.CreateAsync(command, context.RequestAborted);
            return result.IsFailure ? BrowserApiResultMapper.Problem(context, result.Error.Code)
                : Created(context, shopId, result.Value.ShipmentId, result.Value);
        }).WithName("BrowserCreateShipment").Accepts<BrowserShipmentWriteRequest>("application/json").Produces<CreateShipmentResponse>(201);
        group.MapPost("/shops/{shopId:guid}/shipments/drafts", async (HttpContext context, Guid shopId,
            IValidator<CreateDraftShipmentCommand> validator, ICreateDraftShipmentService service, IShopUiActionRateLimiter limiter) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var body = await BrowserApiEndpoints.ReadAsync<BrowserShipmentWriteRequest>(context);
            if (StorageProblem(context, body) is { } invalid) return invalid;
            var command = body.Draft(actor, shopId);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            if (!validation.IsValid) return BrowserValidationProblem.From(context, validation.Errors);
            if (shopId == Guid.Empty) return BrowserApiResultMapper.Problem(context, "Application.ValidationFailed");
            if (Limit(context, limiter, actor) is { } denied) return denied;
            var result = await service.CreateAsync(command, context.RequestAborted);
            return result.IsFailure ? BrowserApiResultMapper.Problem(context, result.Error.Code)
                : Created(context, shopId, result.Value.ShipmentId, result.Value);
        }).WithName("BrowserCreateDraftShipment").Accepts<BrowserShipmentWriteRequest>("application/json").Produces<DraftShipmentResponse>(201);
        group.MapPut("/shops/{shopId:guid}/shipments/{shipmentId:guid}", async (HttpContext context, Guid shopId, Guid shipmentId,
            IValidator<UpdateShipmentBeforePickupCommand> validator, IUpdateShipmentBeforePickupService service) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var body = await BrowserApiEndpoints.ReadAsync<BrowserShipmentWriteRequest>(context);
            if (StorageProblem(context, body) is { } invalid) return invalid;
            var command = body.Update(actor, shopId, shipmentId);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            if (!validation.IsValid) return BrowserValidationProblem.From(context, validation.Errors);
            if (shopId == Guid.Empty) return BrowserApiResultMapper.Problem(context, "Application.ValidationFailed");
            return BrowserApiResultMapper.From(context, await service.UpdateAsync(command, context.RequestAborted));
        }).WithName("BrowserUpdateShipment").Accepts<BrowserShipmentWriteRequest>("application/json").Produces<DraftShipmentResponse>();
        group.MapPost("/shops/{shopId:guid}/shipments/{shipmentId:guid}/submit", async (HttpContext context, Guid shopId, Guid shipmentId,
            IValidator<SubmitDraftShipmentCommand> validator, ISubmitDraftShipmentService service) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var command = new SubmitDraftShipmentCommand(actor, shipmentId, shopId);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            if (!validation.IsValid) return BrowserValidationProblem.From(context, validation.Errors);
            if (shopId == Guid.Empty) return BrowserApiResultMapper.Problem(context, "Application.ValidationFailed");
            return BrowserApiResultMapper.From(context, await service.SubmitAsync(command, context.RequestAborted));
        }).WithName("BrowserSubmitDraftShipment").Produces<DraftShipmentResponse>();
        group.MapPost("/shops/{shopId:guid}/shipments/{shipmentId:guid}/cancel", async (HttpContext context, Guid shopId, Guid shipmentId,
            IValidator<CancelShipmentCommand> validator, ICancelShipmentForCurrentShopService service) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var body = await BrowserApiEndpoints.ReadAsync<BrowserShipmentCancelRequest>(context);
            var command = new CancelShipmentCommand(actor, shipmentId, body.Reason, shopId);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            if (!validation.IsValid) return BrowserValidationProblem.From(context, validation.Errors);
            if (shopId == Guid.Empty) return BrowserApiResultMapper.Problem(context, "Application.ValidationFailed");
            var result = await service.CancelAsync(command, context.RequestAborted);
            return result.IsSuccess ? Results.NoContent() : BrowserApiResultMapper.Problem(context, result.Error.Code);
        }).WithName("BrowserCancelShipment").Accepts<BrowserShipmentCancelRequest>("application/json").Produces(204);
    }
    private static IResult? StorageProblem(HttpContext context, BrowserShipmentWriteRequest body)
    {
        var failures = BrowserShipmentStorageLimits.Validate(body.WeightKg, body.LengthCm, body.WidthCm, body.HeightCm, body.GoodsValueAmount, body.CodAmount);
        return failures.Count > 0 ? BrowserValidationProblem.From(context, failures) : null;
    }
    private static IResult Created<T>(HttpContext context, Guid shop, Guid shipment, T value)
    {
        context.Response.Headers.Location = $"/api/v1/app/shops/{shop}/shipments/{shipment}";
        return Results.Json(value, BrowserApiJsonOptions.Value, statusCode: 201);
    }
    private static IResult? Limit(HttpContext context, IShopUiActionRateLimiter limiter, Guid actor)
    {
        if (limiter.TryAcquire(actor, ShopUiActionKind.CreateShipment, out var retry)) return null;
        context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return BrowserApiResultMapper.Problem(context, "Browser.RateLimitExceeded");
    }
}
