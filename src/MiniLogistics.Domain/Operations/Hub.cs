using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Operations;

/// <summary>
/// Represents the Hub domain entity.
/// </summary>
public sealed class Hub : AuditableEntity
{
    private Hub()
    {
        Code = string.Empty;
        Name = string.Empty;
        Province = string.Empty;
        Country = "Vietnam";
    }

    public Hub(
        string code,
        string name,
        string province,
        DateTimeOffset createdAtUtc,
        string? ward = null,
        string? addressLine = null,
        bool isRegionalSortingHub = false,
        string country = "Vietnam")
        : base(Guid.NewGuid(), createdAtUtc)
    {
        Code = NormalizeCode(code);
        Name = DomainGuard.RequireText(name, nameof(name));
        Province = DomainGuard.RequireText(province, nameof(province));
        Ward = NormalizeOptional(ward);
        AddressLine = NormalizeOptional(addressLine);
        Country = DomainGuard.RequireText(country, nameof(country));
        IsRegionalSortingHub = isRegionalSortingHub;
        IsActive = true;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string Province { get; private set; }

    public string? Ward { get; private set; }

    public string? AddressLine { get; private set; }

    public string Country { get; private set; }

    public bool IsRegionalSortingHub { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateProfile(
        string code,
        string name,
        string province,
        DateTimeOffset updatedAtUtc,
        string? ward = null,
        string? addressLine = null,
        bool isRegionalSortingHub = false,
        string country = "Vietnam")
    {
        Code = NormalizeCode(code);
        Name = DomainGuard.RequireText(name, nameof(name));
        Province = DomainGuard.RequireText(province, nameof(province));
        Ward = NormalizeOptional(ward);
        AddressLine = NormalizeOptional(addressLine);
        Country = DomainGuard.RequireText(country, nameof(country));
        IsRegionalSortingHub = isRegionalSortingHub;
        MarkUpdated(updatedAtUtc);
    }

    public void Rename(string name, DateTimeOffset updatedAtUtc)
    {
        Name = DomainGuard.RequireText(name, nameof(name));
        MarkUpdated(updatedAtUtc);
    }

    public void UpdateLocation(
        string province,
        DateTimeOffset updatedAtUtc,
        string? ward = null,
        string? addressLine = null,
        string country = "Vietnam")
    {
        Province = DomainGuard.RequireText(province, nameof(province));
        Ward = NormalizeOptional(ward);
        AddressLine = NormalizeOptional(addressLine);
        Country = DomainGuard.RequireText(country, nameof(country));
        MarkUpdated(updatedAtUtc);
    }

    public void Activate(DateTimeOffset updatedAtUtc)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        MarkUpdated(updatedAtUtc);
    }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated(updatedAtUtc);
    }

    private static string NormalizeCode(string value)
    {
        return DomainGuard.RequireText(value, nameof(value)).ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
