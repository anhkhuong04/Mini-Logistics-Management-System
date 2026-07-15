using FluentValidation;

namespace MiniLogistics.Application.Shops.CreateAdditionalShop;

public sealed class CreateAdditionalShopCommandValidator : AbstractValidator<CreateAdditionalShopCommand>
{
    public CreateAdditionalShopCommandValidator()
    {
        RuleFor(command => command.CurrentUserId)
            .NotEmpty()
            .WithMessage("Current user id is required.");

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.PhoneNumber)
            .NotEmpty()
            .Must(BeValidPhoneNumber)
            .WithMessage("Phone number is invalid.");

        RuleFor(command => command.AddressLine)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(command => command.Ward)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Province)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Country)
            .NotEmpty()
            .MaximumLength(100);
    }

    private static bool BeValidPhoneNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        var startsWithPlus = normalized.StartsWith('+');
        var digits = startsWithPlus ? normalized[1..] : normalized;

        return digits.Length is >= 9 and <= 15
            && digits.All(char.IsDigit);
    }
}
