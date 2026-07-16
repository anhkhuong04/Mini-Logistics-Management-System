using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Operations;

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
        string? ward = null,
        string? addressLine = null,
        bool isRegionalSortingHub = false,
        string country = "Vietnam")
        : base(Guid.NewGuid())
    {
        Code = NormalizeCode(code);
        Name = RequireText(name, nameof(name));
        Province = RequireText(province, nameof(province));
        Ward = NormalizeOptional(ward);
        AddressLine = NormalizeOptional(addressLine);
        Country = RequireText(country, nameof(country));
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
        string? ward = null,
        string? addressLine = null,
        bool isRegionalSortingHub = false,
        string country = "Vietnam")
    {
        Code = NormalizeCode(code);
        Name = RequireText(name, nameof(name));
        Province = RequireText(province, nameof(province));
        Ward = NormalizeOptional(ward);
        AddressLine = NormalizeOptional(addressLine);
        Country = RequireText(country, nameof(country));
        IsRegionalSortingHub = isRegionalSortingHub;
        MarkUpdated();
    }

    public void Rename(string name)
    {
        Name = RequireText(name, nameof(name));
        MarkUpdated();
    }

    public void UpdateLocation(
        string province,
        string? ward = null,
        string? addressLine = null,
        string country = "Vietnam")
    {
        Province = RequireText(province, nameof(province));
        Ward = NormalizeOptional(ward);
        AddressLine = NormalizeOptional(addressLine);
        Country = RequireText(country, nameof(country));
        MarkUpdated();
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        MarkUpdated();
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated();
    }

    private static string NormalizeCode(string value)
    {
        return RequireText(value, nameof(value)).ToUpperInvariant();
    }

    private static string RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
