using FluentValidation;

namespace MiniLogistics.Application.AdminUsers.SetShipperCapacity;

public sealed class SetShipperCapacityCommandValidator : AbstractValidator<SetShipperCapacityCommand>
{
    public SetShipperCapacityCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty()
            .WithMessage("Requester user id is required.");

        RuleFor(command => command.ShipperId)
            .NotEmpty()
            .WithMessage("Shipper user id is required.");

        RuleFor(command => command.MaxActiveShipments)
            .InclusiveBetween(1, 200)
            .WithMessage("Max active shipments must be between 1 and 200.");
    }
}
