using FluentValidation;

namespace MiniLogistics.Application.AdminHubs.SetHubActiveStatus;

public sealed class SetHubActiveStatusCommandValidator : AbstractValidator<SetHubActiveStatusCommand>
{
    public SetHubActiveStatusCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.HubId)
            .NotEmpty();

        RuleFor(command => command.Reason)
            .MaximumLength(500);
    }
}
