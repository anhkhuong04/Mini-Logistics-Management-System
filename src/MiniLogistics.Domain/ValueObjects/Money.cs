using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.ValueObjects;

/// <summary>
/// Represents the validated Money value used by the domain model.
/// </summary>
public sealed record Money
{
    public static readonly Money Zero = new(0);

    public Money(decimal amount, string currency = "VND")
    {
        if (amount < 0)
        {
            throw new DomainException("Money amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainException("Currency is required.");
        }

        Amount = decimal.Round(amount, 2);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public bool IsZero => Amount == 0;

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        if (multiplier < 0)
        {
            throw new DomainException("Money multiplier cannot be negative.");
        }

        return new Money(money.Amount * multiplier, money.Currency);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new DomainException("Cannot operate on money values with different currencies.");
        }
    }
}
