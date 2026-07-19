using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Domain.Tests;

public sealed class ShipmentStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ShopId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OperatorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ShipperId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void AssignedShipment_CanMoveThroughDeliveryLifecycle()
    {
        var shipment = CreateShipment();

        var assignResult = shipment.AssignShipper(ShipperId, OperatorId, Now.AddMinutes(1), "Assigned.");
        Assert.True(assignResult.IsSuccess, assignResult.Error.Description);

        AssertStatusChange(shipment, ShipmentStatus.PickingUp, ShipperId, Now.AddMinutes(2));
        AssertStatusChange(shipment, ShipmentStatus.PickedUp, ShipperId, Now.AddMinutes(3));
        AssertStatusChange(shipment, ShipmentStatus.InTransit, ShipperId, Now.AddMinutes(4));
        AssertStatusChange(shipment, ShipmentStatus.Delivering, ShipperId, Now.AddMinutes(5));
        AssertStatusChange(shipment, ShipmentStatus.Delivered, ShipperId, Now.AddMinutes(6));

        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.Equal(7, shipment.StatusHistory.Count);
    }

    [Fact]
    public void PendingShipment_CannotJumpDirectlyToDelivered()
    {
        var shipment = CreateShipment();

        var result = shipment.UpdateStatus(
            ShipmentStatus.Delivered,
            OperatorId,
            Now.AddMinutes(1),
            "Invalid jump.");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.InvalidStatusTransition, result.Error);
        Assert.Equal(ShipmentStatus.PendingPickup, shipment.Status);
    }

    [Fact]
    public void DeliveryFailed_RequiresReason()
    {
        var shipment = CreateShipment();
        MoveToDelivering(shipment);

        var missingReason = shipment.UpdateStatus(
            ShipmentStatus.DeliveryFailed,
            ShipperId,
            Now.AddMinutes(10));
        var withReason = shipment.UpdateStatus(
            ShipmentStatus.DeliveryFailed,
            ShipperId,
            Now.AddMinutes(11),
            "Customer unavailable.");

        Assert.True(missingReason.IsFailure);
        Assert.Equal(ShipmentErrors.DeliveryFailedNoteRequired, missingReason.Error);
        Assert.True(withReason.IsSuccess, withReason.Error.Description);
        Assert.Equal(ShipmentStatus.DeliveryFailed, shipment.Status);
    }

    [Fact]
    public void TerminalShipment_CannotChangeStatus()
    {
        var shipment = CreateShipment();
        MoveToDelivered(shipment);

        var result = shipment.UpdateStatus(
            ShipmentStatus.Returned,
            ShipperId,
            Now.AddMinutes(20),
            "Too late.");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.CompletedShipmentCannotChange, result.Error);
    }

    [Fact]
    public void PickedUpShipment_CannotBeCancelled()
    {
        var shipment = CreateShipment();
        Assert.True(shipment.AssignShipper(ShipperId, OperatorId, Now.AddMinutes(1), "Assigned.").IsSuccess);
        AssertStatusChange(shipment, ShipmentStatus.PickingUp, ShipperId, Now.AddMinutes(2));
        AssertStatusChange(shipment, ShipmentStatus.PickedUp, ShipperId, Now.AddMinutes(3));

        var result = shipment.Cancel(OperatorId, Now.AddMinutes(4), "Customer cancelled.");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.CannotCancel, result.Error);
        Assert.Equal(ShipmentStatus.PickedUp, shipment.Status);
    }

    [Fact]
    public void DraftShipment_CanBeSubmittedOnce()
    {
        var shipment = CreateDraftShipment();

        var submitResult = shipment.SubmitDraft(OperatorId, Now.AddMinutes(1));
        var secondSubmitResult = shipment.SubmitDraft(OperatorId, Now.AddMinutes(2));

        Assert.True(submitResult.IsSuccess, submitResult.Error.Description);
        Assert.Equal(ShipmentStatus.PendingPickup, shipment.Status);
        Assert.True(secondSubmitResult.IsFailure);
        Assert.Equal(ShipmentErrors.OnlyDraftCanBeSubmitted, secondSubmitResult.Error);
    }

    [Fact]
    public void CompleteCodCollection_DeactivatesAssignmentsOnlyAfterDelivered()
    {
        var shipment = CreateShipment();
        var beforeDelivered = shipment.CompleteCodCollection(Now.AddMinutes(1));
        MoveToDelivered(shipment);

        Assert.True(beforeDelivered.IsFailure);
        Assert.Equal(ShipmentErrors.CodCollectionRequiresDeliveredShipment, beforeDelivered.Error);
        Assert.Contains(shipment.Assignments, assignment => assignment.IsActive);

        var completed = shipment.CompleteCodCollection(Now.AddMinutes(11));

        Assert.True(completed.IsSuccess, completed.Error.Description);
        Assert.DoesNotContain(shipment.Assignments, assignment => assignment.IsActive);
    }

    private static void MoveToDelivered(Shipment shipment)
    {
        MoveToDelivering(shipment);
        AssertStatusChange(shipment, ShipmentStatus.Delivered, ShipperId, Now.AddMinutes(10));
    }

    private static void MoveToDelivering(Shipment shipment)
    {
        Assert.True(shipment.AssignShipper(ShipperId, OperatorId, Now.AddMinutes(1), "Assigned.").IsSuccess);
        AssertStatusChange(shipment, ShipmentStatus.PickingUp, ShipperId, Now.AddMinutes(2));
        AssertStatusChange(shipment, ShipmentStatus.PickedUp, ShipperId, Now.AddMinutes(3));
        AssertStatusChange(shipment, ShipmentStatus.InTransit, ShipperId, Now.AddMinutes(4));
        AssertStatusChange(shipment, ShipmentStatus.Delivering, ShipperId, Now.AddMinutes(5));
    }

    private static void AssertStatusChange(
        Shipment shipment,
        ShipmentStatus status,
        Guid changedByUserId,
        DateTimeOffset changedAtUtc)
    {
        var result = shipment.UpdateStatus(status, changedByUserId, changedAtUtc, $"Move to {status}.");
        Assert.True(result.IsSuccess, result.Error.Description);
    }

    private static Shipment CreateShipment()
    {
        return CreateShipment(isDraft: false);
    }

    private static Shipment CreateDraftShipment()
    {
        return CreateShipment(isDraft: true);
    }

    private static Shipment CreateShipment(bool isDraft)
    {
        return isDraft
            ? Shipment.CreateDraft(
                ShopId,
                "Shop Demo",
                new PhoneNumber("0900000000"),
                "Customer Demo",
                new PhoneNumber("0911111111"),
                new Address("1 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"),
                new Address("9 Le Loi", "Ben Nghe", "Ho Chi Minh"),
                new Weight(1m),
                new ParcelDimensions(10m, 10m, 10m),
                new Weight(1m),
                new Money(500_000m),
                new Money(100_000m),
                new ShippingFeeBreakdown(new Money(20_000m), Money.Zero, Money.Zero, Money.Zero),
                RouteType.IntraProvince,
                OperatorId,
                Now,
                "Domain test shipment.",
                new TrackingCode("ML202607190000000001"))
            : Shipment.Create(
                ShopId,
                "Shop Demo",
                new PhoneNumber("0900000000"),
                "Customer Demo",
                new PhoneNumber("0911111111"),
                new Address("1 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"),
                new Address("9 Le Loi", "Ben Nghe", "Ho Chi Minh"),
                new Weight(1m),
                new ParcelDimensions(10m, 10m, 10m),
                new Weight(1m),
                new Money(500_000m),
                new Money(100_000m),
                new ShippingFeeBreakdown(new Money(20_000m), Money.Zero, Money.Zero, Money.Zero),
                RouteType.IntraProvince,
                OperatorId,
                Now,
                "Domain test shipment.",
                new TrackingCode("ML202607190000000001"));
    }
}
