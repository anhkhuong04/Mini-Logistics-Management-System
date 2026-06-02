using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.ValueObjects;

public sealed record PhoneNumber
{
    public PhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Phone number is required.");
        }

        var normalized = value.Trim().Replace(" ", string.Empty);
        var startsWithPlus = normalized.StartsWith('+');
        var digits = startsWithPlus ? normalized[1..] : normalized;

        if (digits.Length is < 9 or > 15 || digits.Any(character => !char.IsDigit(character)))
        {
            throw new DomainException("Phone number is invalid.");
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
