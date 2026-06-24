using FluentValidation;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers.CreateInternalUser;

public sealed class CreateInternalUserCommandValidator : AbstractValidator<CreateInternalUserCommand>
{
    public CreateInternalUserCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.FullName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(command => command.PhoneNumber)
            .NotEmpty()
            .Must(BeValidPhoneNumber)
            .WithMessage("Phone number is invalid.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(100);

        RuleFor(command => command.Role)
            .Must(BeInternalRole)
            .WithMessage("Role must be Shipper or Operator.");
    }

    private static bool BeInternalRole(string? role)
    {
        return string.Equals(role, nameof(UserRole.Shipper), StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, nameof(UserRole.Operator), StringComparison.OrdinalIgnoreCase);
    }

    private static bool BeValidPhoneNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace(" ", string.Empty);
        var startsWithPlus = normalized.StartsWith('+');
        var digits = startsWithPlus ? normalized[1..] : normalized;

        return digits.Length is >= 9 and <= 15
            && digits.All(char.IsDigit);
    }
}
