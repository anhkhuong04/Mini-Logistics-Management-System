using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shops;

public sealed class ShopStaffMembership : AuditableEntity
{
    private ShopStaffMembership()
    {
    }

    public ShopStaffMembership(
        Guid shopId,
        Guid userId,
        Guid createdByOwnerUserId,
        ShopStaffRole role,
        ShopPermission permissions,
        DateTimeOffset createdAtUtc)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (shopId == Guid.Empty || userId == Guid.Empty || createdByOwnerUserId == Guid.Empty)
        {
            throw new DomainException("Shop, staff user, and creating owner are required.");
        }

        ShopId = shopId;
        UserId = userId;
        CreatedByOwnerUserId = createdByOwnerUserId;
        IsActive = true;
        SetAccess(role, permissions, createdAtUtc);
        UpdatedAtUtc = null;
    }

    public Guid ShopId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid CreatedByOwnerUserId { get; private set; }

    public ShopStaffRole Role { get; private set; }

    public ShopPermission Permissions { get; private set; }

    public bool IsActive { get; private set; }

    public bool HasPermission(ShopPermission permission) =>
        (Permissions & permission) == permission;

    public void SetAccess(
        ShopStaffRole role,
        ShopPermission permissions,
        DateTimeOffset updatedAtUtc)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException("Shop staff role is invalid.");
        }

        if ((permissions & ShopPermission.ViewShipments) == 0
            || (permissions & ~ShopPermission.All) != 0)
        {
            throw new DomainException("Shop staff permissions must include ViewShipments and contain only supported values.");
        }

        Role = role;
        Permissions = permissions;
        MarkUpdated(updatedAtUtc);
    }

    public void SetActive(bool isActive, DateTimeOffset updatedAtUtc)
    {
        IsActive = isActive;
        MarkUpdated(updatedAtUtc);
    }
}
