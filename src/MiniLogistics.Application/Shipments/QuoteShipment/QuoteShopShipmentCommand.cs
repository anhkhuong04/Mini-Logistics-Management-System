using FluentValidation;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.QuoteShipment;

public sealed record QuoteShopShipmentCommand(Guid UserId, Guid ShopId, ShipmentAddressDto PickupAddress,
    ShipmentAddressDto DeliveryAddress, decimal WeightKg, decimal LengthCm, decimal WidthCm, decimal HeightCm,
    decimal GoodsValueAmount, string Currency = "VND");

public sealed record ShopShippingQuoteResponse(RouteType RouteType, decimal ActualWeightKg, decimal VolumetricWeightKg,
    decimal ChargeableWeightKg, decimal BaseFeeAmount, decimal ExtraWeightFeeAmount, decimal InsuranceFeeAmount,
    decimal ReturnFeeAmount, decimal ShippingFeeAmount, string Currency);

public sealed class QuoteShopShipmentCommandValidator : AbstractValidator<QuoteShopShipmentCommand>
{
    public QuoteShopShipmentCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ShopId).NotEmpty();
        RuleFor(x => x.PickupAddress).NotNull().SetValidator(new ShipmentAddressDtoValidator());
        RuleFor(x => x.DeliveryAddress).NotNull().SetValidator(new ShipmentAddressDtoValidator());
        RuleFor(x => x.WeightKg).GreaterThan(0);
        RuleFor(x => x.LengthCm).GreaterThan(0);
        RuleFor(x => x.WidthCm).GreaterThan(0);
        RuleFor(x => x.HeightCm).GreaterThan(0);
        RuleFor(x => x.GoodsValueAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
