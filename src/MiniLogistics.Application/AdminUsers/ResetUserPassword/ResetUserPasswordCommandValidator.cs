using FluentValidation;
using MiniLogistics.Application.Identity;

namespace MiniLogistics.Application.AdminUsers.ResetUserPassword;

public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.TargetUserId)
            .NotEmpty();

        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MaximumLength(PasswordPolicy.MaximumLength)
            .Must(PasswordPolicy.MeetsComplexity)
            .WithMessage(PasswordPolicy.RequirementMessage);

        RuleFor(command => command.Reason)
            .MaximumLength(500);
    }
}
