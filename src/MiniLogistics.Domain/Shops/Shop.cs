using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Shops;

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
        Address address)
        : base(Guid.NewGuid())
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new DomainException("Owner user id is required.");
        }

        OwnerUserId = ownerUserId;
        Name = RequireText(name, nameof(name));
        PhoneNumber = phoneNumber;
        Address = address;
        IsActive = true;
    }

    public Guid OwnerUserId { get; private set; }

    public string Name { get; private set; }

    public PhoneNumber PhoneNumber { get; private set; }

    public Address Address { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string name)
    {
        Name = RequireText(name, nameof(name));
        MarkUpdated();
    }

    public void UpdateContact(PhoneNumber phoneNumber, Address address)
    {
        PhoneNumber = phoneNumber;
        Address = address;
        MarkUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    private static string RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        return value.Trim();
    }
}
