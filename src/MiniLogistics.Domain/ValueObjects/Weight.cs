using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.ValueObjects;

public sealed record Weight
{
    public Weight(decimal kilograms)
    {
        if (kilograms <= 0)
        {
            throw new DomainException("Weight must be greater than zero.");
        }

        Kilograms = decimal.Round(kilograms, 3);
    }

    public decimal Kilograms { get; }
}
