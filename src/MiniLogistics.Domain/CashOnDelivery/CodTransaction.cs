using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.CashOnDelivery;

public sealed class CodTransaction : AuditableEntity
{
    private CodTransaction()
    {
        Amount = Money.Zero;
    }

    private CodTransaction(Guid shipmentId, Money amount)
        : base(Guid.NewGuid())
    {
        if (shipmentId == Guid.Empty)
        {
            throw new DomainException("Shipment id is required.");
        }

        ShipmentId = shipmentId;
        Amount = amount;
        Status = amount.IsZero ? CodStatus.NotRequired : CodStatus.PendingCollection;
    }

    public Guid ShipmentId { get; private set; }

    public Money Amount { get; private set; }

    public CodStatus Status { get; private set; }

    public DateTimeOffset? CollectedAtUtc { get; private set; }

    public Guid? CollectedByUserId { get; private set; }

    public DateTimeOffset? SettledAtUtc { get; private set; }

    public Guid? SettledByUserId { get; private set; }

    public static CodTransaction Create(Guid shipmentId, Money amount)
    {
        return new CodTransaction(shipmentId, amount);
    }

    public Result UpdateAmount(Money amount)
    {
        if (Status is not (CodStatus.NotRequired or CodStatus.PendingCollection))
        {
            return Result.Failure(CodErrors.CannotChangeAmount);
        }

        Amount = amount;
        Status = amount.IsZero ? CodStatus.NotRequired : CodStatus.PendingCollection;
        MarkUpdated();

        return Result.Success();
    }

    public Result MarkCollected(ShipmentStatus shipmentStatus, Guid collectedByUserId)
    {
        if (Status == CodStatus.NotRequired)
        {
            return Result.Failure(CodErrors.CollectionNotRequired);
        }

        if (Status != CodStatus.PendingCollection)
        {
            return Result.Failure(CodErrors.CannotCollect);
        }

        if (shipmentStatus != ShipmentStatus.Delivered)
        {
            return Result.Failure(CodErrors.ShipmentMustBeDelivered);
        }

        Status = CodStatus.Collected;
        CollectedAtUtc = DateTimeOffset.UtcNow;
        CollectedByUserId = collectedByUserId;
        MarkUpdated();

        return Result.Success();
    }

    public Result MarkSettled(Guid settledByUserId)
    {
        if (Status != CodStatus.Collected)
        {
            return Result.Failure(CodErrors.CannotSettle);
        }

        Status = CodStatus.Settled;
        SettledAtUtc = DateTimeOffset.UtcNow;
        SettledByUserId = settledByUserId;
        MarkUpdated();

        return Result.Success();
    }
}
