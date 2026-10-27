using FluentValidation;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shipments.QuoteShipment;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserShippingQuoteRequest(ShipmentAddressDto PickupAddress, ShipmentAddressDto DeliveryAddress,
    decimal WeightKg, decimal LengthCm, decimal WidthCm, decimal HeightCm, decimal GoodsValueAmount, string Currency = "VND")
{
    public QuoteShopShipmentCommand ToCommand(Guid actor, Guid shop) => new(actor, shop, PickupAddress, DeliveryAddress,
        WeightKg, LengthCm, WidthCm, HeightCm, GoodsValueAmount, Currency);
}

public static class BrowserShippingQuoteEndpoints
{
    public static void MapBrowserShippingQuote(this RouteGroupBuilder group) => group.MapPost("/shops/{shopId:guid}/shipping/quote",
        async (HttpContext context, Guid shopId, IValidator<QuoteShopShipmentCommand> validator, IQuoteShopShipmentService service) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var request = await BrowserApiEndpoints.ReadAsync<BrowserShippingQuoteRequest>(context);
            var command = request.ToCommand(actor, shopId);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            if (!validation.IsValid)
                return BrowserValidationProblem.From(context, validation.Errors);
            var limits = BrowserShipmentStorageLimits.Validate(request.WeightKg, request.LengthCm, request.WidthCm, request.HeightCm, request.GoodsValueAmount);
            if (limits.Count > 0)
                return BrowserValidationProblem.From(context, limits);
            return BrowserApiResultMapper.From(context, await service.QuoteAsync(command, context.RequestAborted));
        }).WithName("BrowserShopShippingQuote").Accepts<BrowserShippingQuoteRequest>("application/json").Produces<ShopShippingQuoteResponse>();
}
