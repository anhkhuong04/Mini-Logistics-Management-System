using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Operations;

/// <summary>
/// Represents the Route Region Config domain entity.
/// </summary>
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
        DateTimeOffset createdAtUtc,
        int version = 1)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (version <= 0)
        {
            throw new DomainException("Route region config version must be greater than zero.");
        }

        Province = DomainGuard.RequireText(province, nameof(province), 100);
        Region = DomainGuard.RequireText(region, nameof(region), 120);
        Version = version;
        IsActive = true;
    }

    public string Province { get; private set; }

    public string Region { get; private set; }

    public int Version { get; private set; }

    public bool IsActive { get; private set; }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated(updatedAtUtc);
    }

}
