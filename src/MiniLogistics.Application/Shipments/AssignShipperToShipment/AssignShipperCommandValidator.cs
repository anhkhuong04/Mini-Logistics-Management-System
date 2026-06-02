using FluentValidation;

namespace MiniLogistics.Application.Shipments.AssignShipperToShipment;

public sealed class AssignShipperCommandValidator : AbstractValidator<AssignShipperCommand>
{
    public AssignShipperCommandValidator()
    {
        RuleFor(command => command.ShipmentId)
            .NotEmpty();

        RuleFor(command => command.ShipperId)
            .NotEmpty();

        RuleFor(command => command.AssignedByUserId)
            .NotEmpty();

        RuleFor(command => command.Note)
            .MaximumLength(500);
    }
}
