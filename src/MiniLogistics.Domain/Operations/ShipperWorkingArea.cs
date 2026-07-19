using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Operations;

/// <summary>
/// Represents the Shipper Working Area domain entity.
/// </summary>
public sealed class ShipperWorkingArea : AuditableEntity
{
    private ShipperWorkingArea()
    {
        Province = string.Empty;
    }

    public ShipperWorkingArea(
        Guid shipperId,
        Guid hubId,
        string province,
        DateTimeOffset createdAtUtc,
        string? ward = null,
        string? zoneCode = null)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (shipperId == Guid.Empty)
        {
            throw new DomainException("Shipper id is required.");
        }

        if (hubId == Guid.Empty)
        {
            throw new DomainException("Hub id is required.");
        }

        ShipperId = shipperId;
        HubId = hubId;
        Province = DomainGuard.RequireText(province, nameof(province));
        Ward = NormalizeOptional(ward);
        ZoneCode = NormalizeOptional(zoneCode);
        IsActive = true;
    }

    public Guid ShipperId { get; private set; }

    public Guid HubId { get; private set; }

    public string Province { get; private set; }

    public string? Ward { get; private set; }

    public string? ZoneCode { get; private set; }

    public bool IsActive { get; private set; }

    public bool Matches(Guid hubId, string? ward, string? zoneCode)
    {
        return HubId == hubId
            && string.Equals(NormalizeOptional(Ward), NormalizeOptional(ward), StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeOptional(ZoneCode), NormalizeOptional(zoneCode), StringComparison.OrdinalIgnoreCase);
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
