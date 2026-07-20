using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.CashOnDelivery;

/// <summary>
/// Represents the Cod Transaction domain entity.
/// </summary>
public sealed class CodTransaction : AuditableEntity
{
    private CodTransaction()
    {
        Amount = Money.Zero;
        CollectionNote = string.Empty;
    }

    private CodTransaction(Guid shipmentId, Money amount, DateTimeOffset createdAtUtc)
        : base(Guid.NewGuid(), createdAtUtc)
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

    public Money? CollectedAmount { get; private set; }

    public Money? DiscrepancyAmount { get; private set; }

    public string CollectionNote { get; private set; } = string.Empty;

    public CodStatus Status { get; private set; }

    public DateTimeOffset? CollectedAtUtc { get; private set; }

    public Guid? CollectedByUserId { get; private set; }

    public DateTimeOffset? SettledAtUtc { get; private set; }

    public Guid? SettledByUserId { get; private set; }

    public static CodTransaction Create(Guid shipmentId, Money amount, DateTimeOffset createdAtUtc)
    {
        return new CodTransaction(shipmentId, amount, createdAtUtc);
    }

    public Result UpdateAmount(Money amount, DateTimeOffset updatedAtUtc)
    {
        if (Status is not (CodStatus.NotRequired or CodStatus.PendingCollection))
        {
            return Result.Failure(CodErrors.CannotChangeAmount);
        }

        Amount = amount;
        Status = amount.IsZero ? CodStatus.NotRequired : CodStatus.PendingCollection;
        MarkUpdated(updatedAtUtc);

        return Result.Success();
    }

    public Result MarkCollected(
        ShipmentStatus shipmentStatus,
        Guid collectedByUserId,
        DateTimeOffset collectedAtUtc,
        Money? collectedAmount = null,
        string? collectionNote = null)
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

        var actualCollectedAmount = collectedAmount ?? Amount;
        if (actualCollectedAmount.Currency != Amount.Currency)
        {
            return Result.Failure(CodErrors.CollectedAmountCurrencyMismatch);
        }

        var discrepancyAmount = Math.Abs(actualCollectedAmount.Amount - Amount.Amount);
        if (discrepancyAmount > 0 && string.IsNullOrWhiteSpace(collectionNote))
        {
            return Result.Failure(CodErrors.DiscrepancyNoteRequired);
        }

        Status = CodStatus.Collected;
        CollectedAmount = actualCollectedAmount;
        DiscrepancyAmount = new Money(discrepancyAmount, Amount.Currency);
        CollectionNote = collectionNote?.Trim() ?? string.Empty;
        CollectedAtUtc = collectedAtUtc;
        CollectedByUserId = collectedByUserId;
        MarkUpdated(collectedAtUtc);

        return Result.Success();
    }

    public Result MarkSettled(Guid settledByUserId, DateTimeOffset settledAtUtc)
    {
        if (Status != CodStatus.Collected)
        {
            return Result.Failure(CodErrors.CannotSettle);
        }

        Status = CodStatus.Settled;
        SettledAtUtc = settledAtUtc;
        SettledByUserId = settledByUserId;
        MarkUpdated(settledAtUtc);

        return Result.Success();
    }
}
