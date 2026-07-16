using FluentValidation;

namespace MiniLogistics.Application.Shipments.ReassignShipment;

public sealed class ReassignShipmentCommandValidator : AbstractValidator<ReassignShipmentCommand>
{
    public ReassignShipmentCommandValidator()
    {
        RuleFor(command => command.ShipmentId)
            .NotEmpty();

        RuleFor(command => command.NewShipperId)
            .NotEmpty();

        RuleFor(command => command.ReassignedByUserId)
            .NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
