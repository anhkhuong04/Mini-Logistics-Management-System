using FluentValidation;

namespace MiniLogistics.Application.Shippers.SetShipperAvailability;

public sealed class SetShipperAvailabilityCommandValidator : AbstractValidator<SetShipperAvailabilityCommand>
{
    public SetShipperAvailabilityCommandValidator()
    {
        RuleFor(command => command.ShipperUserId)
            .NotEmpty();
    }
}
