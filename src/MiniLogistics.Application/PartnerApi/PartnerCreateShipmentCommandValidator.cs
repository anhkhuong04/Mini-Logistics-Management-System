using FluentValidation;
using MiniLogistics.Application.Shipments.CreateShipment;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerCreateShipmentCommandValidator : AbstractValidator<PartnerCreateShipmentCommand>
{
    public PartnerCreateShipmentCommandValidator()
    {
        RuleFor(command => command.ApiClientId)
            .NotEmpty();

        RuleFor(command => command.ShopId)
            .NotEmpty();

        RuleFor(command => command.ExternalOrderId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.SenderName)
            .MaximumLength(100);

        RuleFor(command => command.SenderPhone)
            .Must(value => string.IsNullOrWhiteSpace(value) || BeValidPhoneNumber(value))
            .WithMessage("Sender phone is invalid.");

        RuleFor(command => command.ReceiverName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.ReceiverPhone)
            .NotEmpty()
            .Must(BeValidPhoneNumber)
            .WithMessage("Receiver phone is invalid.");

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

        RuleFor(command => command.Note)
            .MaximumLength(500);
    }

    private static bool BeValidPhoneNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace(" ", string.Empty);
        var startsWithPlus = normalized.StartsWith('+');
        var digits = startsWithPlus ? normalized[1..] : normalized;

        return digits.Length is >= 9 and <= 15
            && digits.All(char.IsDigit);
    }
}
