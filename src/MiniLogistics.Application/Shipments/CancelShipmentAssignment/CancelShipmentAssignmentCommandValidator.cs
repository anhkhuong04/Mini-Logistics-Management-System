using FluentValidation;

namespace MiniLogistics.Application.Shipments.CancelShipmentAssignment;

public sealed class CancelShipmentAssignmentCommandValidator : AbstractValidator<CancelShipmentAssignmentCommand>
{
    public CancelShipmentAssignmentCommandValidator()
    {
        RuleFor(command => command.ShipmentId)
            .NotEmpty();

        RuleFor(command => command.CancelledByUserId)
            .NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
