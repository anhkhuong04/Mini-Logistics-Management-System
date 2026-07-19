using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.ValueObjects;

/// <summary>
/// Represents the validated Address value used by the domain model.
/// </summary>
public sealed record Address
{
    public Address(
        string street,
        string ward,
        string province,
        string country = "Vietnam")
    {
        Street = DomainGuard.RequireText(street, nameof(street));
        Ward = DomainGuard.RequireText(ward, nameof(ward));
        Province = DomainGuard.RequireText(province, nameof(province));
        Country = DomainGuard.RequireText(country, nameof(country));
    }

    public string Street { get; }

    public string Ward { get; }

    public string Province { get; }

    public string Country { get; }

    public string FullAddress => string.Join(", ", Street, Ward, Province, Country);

}
