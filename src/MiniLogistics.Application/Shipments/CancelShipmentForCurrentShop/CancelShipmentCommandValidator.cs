using FluentValidation;

namespace MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;

public sealed class CancelShipmentCommandValidator : AbstractValidator<CancelShipmentCommand>
{
    public CancelShipmentCommandValidator()
    {
        RuleFor(command => command.OwnerUserId)
            .NotEmpty();

        RuleFor(command => command.ShipmentId)
            .NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
