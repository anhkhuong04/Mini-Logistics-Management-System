using FluentValidation;

namespace MiniLogistics.Application.AdminSystemConfiguration;

public sealed class UpsertRouteRegionConfigCommandValidator : AbstractValidator<UpsertRouteRegionConfigCommand>
{
    public UpsertRouteRegionConfigCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.Province)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Region)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(command => command.Reason)
            .MaximumLength(500);
    }
}
