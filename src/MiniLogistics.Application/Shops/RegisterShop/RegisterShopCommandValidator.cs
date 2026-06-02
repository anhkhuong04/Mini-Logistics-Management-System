using FluentValidation;

namespace MiniLogistics.Application.Shops.RegisterShop;

public sealed class RegisterShopCommandValidator : AbstractValidator<RegisterShopCommand>
{
    public RegisterShopCommandValidator()
    {
        RuleFor(command => command.FullName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(100);

        RuleFor(command => command.ShopName)
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

        var normalized = value.Trim().Replace(" ", string.Empty);
        var startsWithPlus = normalized.StartsWith('+');
        var digits = startsWithPlus ? normalized[1..] : normalized;

        return digits.Length is >= 9 and <= 15
            && digits.All(char.IsDigit);
    }
}
