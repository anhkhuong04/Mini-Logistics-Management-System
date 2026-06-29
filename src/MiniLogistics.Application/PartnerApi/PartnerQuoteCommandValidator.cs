using FluentValidation;
using MiniLogistics.Application.Shipments.CreateShipment;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerQuoteCommandValidator : AbstractValidator<PartnerQuoteCommand>
{
    public PartnerQuoteCommandValidator()
    {
        RuleFor(command => command.ApiClientId)
            .NotEmpty();

        RuleFor(command => command.ShopId)
            .NotEmpty();

        RuleFor(command => command.ExternalOrderId)
            .MaximumLength(100);

        RuleFor(command => command.PickupAddress)
            .SetValidator(new ShipmentAddressDtoValidator()!)
            .When(command => command.PickupAddress is not null);

        RuleFor(command => command.DeliveryAddress)
            .NotNull()
            .SetValidator(new ShipmentAddressDtoValidator());

        RuleFor(command => command.WeightKg)
            .GreaterThan(0);

        RuleFor(command => command.LengthCm)
            .GreaterThan(0);

        RuleFor(command => command.WidthCm)
            .GreaterThan(0);

        RuleFor(command => command.HeightCm)
            .GreaterThan(0);

        RuleFor(command => command.GoodsValueAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.CodAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(3);
    }
}
