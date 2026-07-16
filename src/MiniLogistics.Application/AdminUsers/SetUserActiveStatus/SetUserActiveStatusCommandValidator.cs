using FluentValidation;

namespace MiniLogistics.Application.AdminUsers.SetUserActiveStatus;

public sealed class SetUserActiveStatusCommandValidator : AbstractValidator<SetUserActiveStatusCommand>
{
    public SetUserActiveStatusCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.TargetUserId)
            .NotEmpty();

        RuleFor(command => command.Reason)
            .MaximumLength(500);
    }
}
