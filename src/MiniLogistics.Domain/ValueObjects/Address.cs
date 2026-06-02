using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.ValueObjects;

public sealed record Address
{
    public Address(
        string street,
        string ward,
        string province,
        string country = "Vietnam")
    {
        Street = RequireText(street, nameof(street));
        Ward = RequireText(ward, nameof(ward));
        Province = RequireText(province, nameof(province));
        Country = RequireText(country, nameof(country));
    }

    public string Street { get; }

    public string Ward { get; }

    public string Province { get; }

    public string Country { get; }

    public string FullAddress => string.Join(", ", Street, Ward, Province, Country);

    private static string RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        return value.Trim();
    }
}
