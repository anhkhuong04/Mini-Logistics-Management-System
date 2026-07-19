using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

/// <summary>
/// Represents the Integration Management Scope domain entity.
/// </summary>
public sealed class IntegrationManagementScope : AuditableEntity
{
    private IntegrationManagementScope()
    {
        Province = null;
    }

    public IntegrationManagementScope(
        Guid actorUserId,
        DateTimeOffset createdAtUtc,
        Guid? shopId = null,
        string? province = null,
        bool isGlobal = false)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new DomainException("Actor user id is required.");
        }

        if (!isGlobal && !shopId.HasValue && string.IsNullOrWhiteSpace(province))
        {
            throw new DomainException("Integration management scope requires global, shop, or province.");
        }

        ActorUserId = actorUserId;
        ShopId = shopId;
        Province = NormalizeOptional(province);
        IsGlobal = isGlobal;
        IsActive = true;
    }

    public Guid ActorUserId { get; private set; }

    public Guid? ShopId { get; private set; }

    public string? Province { get; private set; }

    public bool IsGlobal { get; private set; }

    public bool IsActive { get; private set; }

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

    public bool Matches(Guid shopId, string shopProvince)
    {
        if (!IsActive)
        {
            return false;
        }

        if (IsGlobal)
        {
            return true;
        }

        if (ShopId.HasValue && ShopId.Value == shopId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(Province)
            && string.Equals(Province, shopProvince, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
