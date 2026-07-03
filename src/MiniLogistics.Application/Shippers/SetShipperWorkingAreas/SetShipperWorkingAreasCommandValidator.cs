using FluentValidation;

namespace MiniLogistics.Application.Shippers.SetShipperWorkingAreas;

public sealed class SetShipperWorkingAreasCommandValidator : AbstractValidator<SetShipperWorkingAreasCommand>
{
    public SetShipperWorkingAreasCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.ShipperId)
            .NotEmpty();

        RuleFor(command => command.Areas)
            .NotNull()
            .Must(areas => areas.Count <= 30)
            .WithMessage("A shipper can be assigned to at most 30 working areas.");

        RuleForEach(command => command.Areas)
            .ChildRules(area =>
            {
                area.RuleFor(item => item.HubId)
                    .NotEmpty();

                area.RuleFor(item => item.Ward)
                    .MaximumLength(100);

                area.RuleFor(item => item.ZoneCode)
                    .MaximumLength(60);
            });
    }
}
