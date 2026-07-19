using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Shops;

/// <summary>
/// Represents the Shop domain entity.
/// </summary>
public sealed class Shop : AuditableEntity
{
    private Shop()
    {
        Name = string.Empty;
        PhoneNumber = null!;
        Address = null!;
    }

    public Shop(
        Guid ownerUserId,
        string name,
        PhoneNumber phoneNumber,
        Address address,
        DateTimeOffset createdAtUtc)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new DomainException("Owner user id is required.");
        }

        OwnerUserId = ownerUserId;
        Name = DomainGuard.RequireText(name, nameof(name));
        PhoneNumber = phoneNumber;
        Address = address;
        IsActive = true;
    }

    public Guid OwnerUserId { get; private set; }

    public string Name { get; private set; }

    public PhoneNumber PhoneNumber { get; private set; }

    public Address Address { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string name, DateTimeOffset updatedAtUtc)
    {
        Name = DomainGuard.RequireText(name, nameof(name));
        MarkUpdated(updatedAtUtc);
    }

    public void UpdateContact(PhoneNumber phoneNumber, Address address, DateTimeOffset updatedAtUtc)
    {
        PhoneNumber = phoneNumber;
        Address = address;
        MarkUpdated(updatedAtUtc);
    }

    public void Activate(DateTimeOffset updatedAtUtc)
    {
        IsActive = true;
        MarkUpdated(updatedAtUtc);
    }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        IsActive = false;
        MarkUpdated(updatedAtUtc);
    }

}
