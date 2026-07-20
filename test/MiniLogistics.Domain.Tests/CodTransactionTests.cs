using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Domain.Tests;

public sealed class CodTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ShipmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void CodTransaction_CanBeCollectedAndSettledAfterDelivery()
    {
        var transaction = CodTransaction.Create(ShipmentId, new Money(100_000m), Now);

        var collectBeforeDelivery = transaction.MarkCollected(
            ShipmentStatus.PendingPickup,
            UserId,
            Now.AddMinutes(1));
        var collectAfterDelivery = transaction.MarkCollected(
            ShipmentStatus.Delivered,
            UserId,
            Now.AddMinutes(2));

        Assert.True(collectBeforeDelivery.IsFailure);
        Assert.Equal(CodErrors.ShipmentMustBeDelivered, collectBeforeDelivery.Error);
        Assert.True(collectAfterDelivery.IsSuccess, collectAfterDelivery.Error.Description);
        Assert.Equal(CodStatus.Collected, transaction.Status);
        Assert.Equal(UserId, transaction.CollectedByUserId);

        var collectAgain = transaction.MarkCollected(
            ShipmentStatus.Delivered,
            UserId,
            Now.AddMinutes(3));
        var settle = transaction.MarkSettled(UserId, Now.AddMinutes(4));
        var updateAfterSettlement = transaction.UpdateAmount(new Money(50_000m), Now.AddMinutes(5));

        Assert.True(collectAgain.IsFailure);
        Assert.Equal(CodErrors.CannotCollect, collectAgain.Error);
        Assert.True(settle.IsSuccess, settle.Error.Description);
        Assert.Equal(CodStatus.Settled, transaction.Status);
        Assert.True(updateAfterSettlement.IsFailure);
        Assert.Equal(CodErrors.CannotChangeAmount, updateAfterSettlement.Error);
    }

    [Fact]
    public void ZeroCodTransaction_DoesNotRequireCollection()
    {
        var transaction = CodTransaction.Create(ShipmentId, Money.Zero, Now);

        var collect = transaction.MarkCollected(
            ShipmentStatus.Delivered,
            UserId,
            Now.AddMinutes(1));

        Assert.Equal(CodStatus.NotRequired, transaction.Status);
        Assert.True(collect.IsFailure);
        Assert.Equal(CodErrors.CollectionNotRequired, collect.Error);
    }

    [Fact]
    public void CodCollection_RecordsActualAmountAndRequiresNoteForDiscrepancy()
    {
        var missingNoteTransaction = CodTransaction.Create(ShipmentId, new Money(100_000m), Now);
        var discrepancyWithoutNote = missingNoteTransaction.MarkCollected(
            ShipmentStatus.Delivered,
            UserId,
            Now.AddMinutes(1),
            new Money(90_000m));

        var transaction = CodTransaction.Create(ShipmentId, new Money(100_000m), Now);
        var collect = transaction.MarkCollected(
            ShipmentStatus.Delivered,
            UserId,
            Now.AddMinutes(2),
            new Money(90_000m),
            "Receiver paid partial COD.");

        Assert.True(discrepancyWithoutNote.IsFailure);
        Assert.Equal(CodErrors.DiscrepancyNoteRequired, discrepancyWithoutNote.Error);
        Assert.True(collect.IsSuccess, collect.Error.Description);
        Assert.Equal(90_000m, transaction.CollectedAmount?.Amount);
        Assert.Equal(10_000m, transaction.DiscrepancyAmount?.Amount);
        Assert.Equal("Receiver paid partial COD.", transaction.CollectionNote);
    }
}
