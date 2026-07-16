using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Operations;

public sealed class RouteRegionConfig : AuditableEntity
{
    private RouteRegionConfig()
    {
        Province = string.Empty;
        Region = string.Empty;
    }

    public RouteRegionConfig(
        string province,
        string region,
        int version = 1)
        : base(Guid.NewGuid())
    {
        if (version <= 0)
        {
            throw new DomainException("Route region config version must be greater than zero.");
        }

        Province = RequireText(province, nameof(province), 100);
        Region = RequireText(region, nameof(region), 120);
        Version = version;
        IsActive = true;
    }

    public string Province { get; private set; }

    public string Region { get; private set; }

    public int Version { get; private set; }

    public bool IsActive { get; private set; }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated();
    }

    private static string RequireText(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
