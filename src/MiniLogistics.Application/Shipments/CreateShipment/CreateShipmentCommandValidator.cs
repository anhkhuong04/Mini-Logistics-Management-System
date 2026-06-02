using FluentValidation;
namespace MiniLogistics.Application.Shipments.CreateShipment;

public sealed class CreateShipmentCommandValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentCommandValidator()
    {
        RuleFor(command => command.CreatedByUserId)
            .NotEmpty();

        RuleFor(command => command.SenderName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.SenderPhone)
            .NotEmpty()
            .Must(BeValidPhoneNumber)
            .WithMessage("Sender phone is invalid.");

        RuleFor(command => command.ReceiverName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.ReceiverPhone)
            .NotEmpty()
            .Must(BeValidPhoneNumber)
            .WithMessage("Receiver phone is invalid.");

        RuleFor(command => command.PickupAddress)
            .NotNull()
            .SetValidator(new ShipmentAddressDtoValidator());

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

public sealed class ShipmentAddressDtoValidator : AbstractValidator<ShipmentAddressDto>
{
    public ShipmentAddressDtoValidator()
    {
        RuleFor(address => address.Street)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(address => address.Ward)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(address => address.Province)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(address => address.Country)
            .NotEmpty()
            .MaximumLength(100);
    }
}
