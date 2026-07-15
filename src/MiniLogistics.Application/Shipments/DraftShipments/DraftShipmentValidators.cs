using FluentValidation;
using MiniLogistics.Application.Shipments.CreateShipment;

namespace MiniLogistics.Application.Shipments.DraftShipments;

public sealed class CreateDraftShipmentCommandValidator : AbstractValidator<CreateDraftShipmentCommand>
{
    public CreateDraftShipmentCommandValidator()
    {
        ShipmentDetailsValidationRules.AddRules(this);
    }
}

public sealed class UpdateShipmentBeforePickupCommandValidator : AbstractValidator<UpdateShipmentBeforePickupCommand>
{
    public UpdateShipmentBeforePickupCommandValidator()
    {
        RuleFor(command => command.ShipmentId)
            .NotEmpty();

        ShipmentDetailsValidationRules.AddRules(this);
    }
}

public sealed class SubmitDraftShipmentCommandValidator : AbstractValidator<SubmitDraftShipmentCommand>
{
    public SubmitDraftShipmentCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.ShipmentId)
            .NotEmpty();
    }
}

internal static class ShipmentDetailsValidationRules
{
    public static void AddRules<TCommand>(AbstractValidator<TCommand> validator)
        where TCommand : IShipmentDetailsCommand
    {
        validator.RuleFor(command => command.UserId)
            .NotEmpty();

        validator.RuleFor(command => command.SenderName)
            .NotEmpty()
            .MaximumLength(100);

        validator.RuleFor(command => command.SenderPhone)
            .NotEmpty()
            .Must(BeValidPhoneNumber)
            .WithMessage("Sender phone is invalid.");

        validator.RuleFor(command => command.ReceiverName)
            .NotEmpty()
            .MaximumLength(100);

        validator.RuleFor(command => command.ReceiverPhone)
            .NotEmpty()
            .Must(BeValidPhoneNumber)
            .WithMessage("Receiver phone is invalid.");

        validator.RuleFor(command => command.PickupAddress)
            .NotNull()
            .SetValidator(new ShipmentAddressDtoValidator());

        validator.RuleFor(command => command.DeliveryAddress)
            .NotNull()
            .SetValidator(new ShipmentAddressDtoValidator());

        validator.RuleFor(command => command.WeightKg)
            .GreaterThan(0);

        validator.RuleFor(command => command.LengthCm)
            .GreaterThan(0);

        validator.RuleFor(command => command.WidthCm)
            .GreaterThan(0);

        validator.RuleFor(command => command.HeightCm)
            .GreaterThan(0);

        validator.RuleFor(command => command.GoodsValueAmount)
            .GreaterThanOrEqualTo(0);

        validator.RuleFor(command => command.CodAmount)
            .GreaterThanOrEqualTo(0);

        validator.RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(3);

        validator.RuleFor(command => command.Note)
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
