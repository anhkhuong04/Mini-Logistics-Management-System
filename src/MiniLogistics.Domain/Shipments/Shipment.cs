using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Shipments;

public sealed class Shipment : AuditableEntity
{
    private readonly List<ShipmentAssignment> _assignments = [];
    private readonly List<ShipmentStatusHistory> _statusHistory = [];

    private Shipment()
    {
        TrackingCode = null!;
        SenderName = string.Empty;
        SenderPhone = null!;
        ReceiverName = string.Empty;
        ReceiverPhone = null!;
        PickupAddress = null!;
        DeliveryAddress = null!;
        Weight = null!;
        ParcelDimensions = null!;
        ChargeableWeight = null!;
        GoodsValue = Money.Zero;
        CodAmount = Money.Zero;
        ShippingFee = Money.Zero;
        ShippingFeeBreakdown = new ShippingFeeBreakdown(Money.Zero, Money.Zero, Money.Zero, Money.Zero);
    }

    private Shipment(
        Guid shopId,
        TrackingCode trackingCode,
        string senderName,
        PhoneNumber senderPhone,
        string receiverName,
        PhoneNumber receiverPhone,
        Address pickupAddress,
        Address deliveryAddress,
        Weight weight,
        ParcelDimensions parcelDimensions,
        Weight chargeableWeight,
        Money goodsValue,
        Money codAmount,
        ShippingFeeBreakdown shippingFeeBreakdown,
        RouteType routeType,
        string? note,
        Guid createdByUserId,
        ShipmentStatus initialStatus,
        string initialHistoryNote)
        : base(Guid.NewGuid())
    {
        if (shopId == Guid.Empty)
        {
            throw new DomainException("Shop id is required.");
        }

        ShopId = shopId;
        TrackingCode = trackingCode;
        SenderName = RequireText(senderName, nameof(senderName));
        SenderPhone = senderPhone;
        ReceiverName = RequireText(receiverName, nameof(receiverName));
        ReceiverPhone = receiverPhone;
        PickupAddress = pickupAddress;
        DeliveryAddress = deliveryAddress;
        Weight = weight;
        ParcelDimensions = parcelDimensions;
        ChargeableWeight = chargeableWeight;
        GoodsValue = goodsValue;
        CodAmount = codAmount;
        ShippingFeeBreakdown = shippingFeeBreakdown;
        ShippingFee = shippingFeeBreakdown.TotalFee;
        RouteType = routeType;
        Note = note?.Trim();
        Status = initialStatus;

        AddStatusHistory(Status, createdByUserId, initialHistoryNote);
    }

    public Guid ShopId { get; private set; }

    public TrackingCode TrackingCode { get; private set; }

    public string SenderName { get; private set; }

    public PhoneNumber SenderPhone { get; private set; }

    public string ReceiverName { get; private set; }

    public PhoneNumber ReceiverPhone { get; private set; }

    public Address PickupAddress { get; private set; }

    public Address DeliveryAddress { get; private set; }

    public Weight Weight { get; private set; }

    public ParcelDimensions ParcelDimensions { get; private set; }

    public Weight ChargeableWeight { get; private set; }

    public Money GoodsValue { get; private set; }

    public Money CodAmount { get; private set; }

    public Money ShippingFee { get; private set; }

    public ShippingFeeBreakdown ShippingFeeBreakdown { get; private set; }

    public RouteType RouteType { get; private set; }

    public string? Note { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public IReadOnlyCollection<ShipmentAssignment> Assignments => _assignments.AsReadOnly();

    public IReadOnlyCollection<ShipmentStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public static Shipment Create(
        Guid shopId,
        string senderName,
        PhoneNumber senderPhone,
        string receiverName,
        PhoneNumber receiverPhone,
        Address pickupAddress,
        Address deliveryAddress,
        Weight weight,
        ParcelDimensions parcelDimensions,
        Weight chargeableWeight,
        Money goodsValue,
        Money codAmount,
        ShippingFeeBreakdown shippingFeeBreakdown,
        RouteType routeType,
        Guid createdByUserId,
        string? note = null,
        TrackingCode? trackingCode = null)
    {
        return new Shipment(
            shopId,
            trackingCode ?? TrackingCode.Generate(),
            senderName,
            senderPhone,
            receiverName,
            receiverPhone,
            pickupAddress,
            deliveryAddress,
            weight,
            parcelDimensions,
            chargeableWeight,
            goodsValue,
            codAmount,
            shippingFeeBreakdown,
            routeType,
            note,
            createdByUserId,
            ShipmentStatus.PendingPickup,
            "Shipment created.");
    }

    public static Shipment CreateDraft(
        Guid shopId,
        string senderName,
        PhoneNumber senderPhone,
        string receiverName,
        PhoneNumber receiverPhone,
        Address pickupAddress,
        Address deliveryAddress,
        Weight weight,
        ParcelDimensions parcelDimensions,
        Weight chargeableWeight,
        Money goodsValue,
        Money codAmount,
        ShippingFeeBreakdown shippingFeeBreakdown,
        RouteType routeType,
        Guid createdByUserId,
        string? note = null,
        TrackingCode? trackingCode = null)
    {
        return new Shipment(
            shopId,
            trackingCode ?? TrackingCode.Generate(),
            senderName,
            senderPhone,
            receiverName,
            receiverPhone,
            pickupAddress,
            deliveryAddress,
            weight,
            parcelDimensions,
            chargeableWeight,
            goodsValue,
            codAmount,
            shippingFeeBreakdown,
            routeType,
            note,
            createdByUserId,
            ShipmentStatus.Draft,
            "Draft created.");
    }

    public Result AssignShipper(Guid shipperId, Guid assignedByUserId, string? note = null)
    {
        if (Status != ShipmentStatus.PendingPickup)
        {
            return Result.Failure(ShipmentErrors.CannotAssign);
        }

        if (shipperId == Guid.Empty)
        {
            return Result.Failure(ShipmentErrors.InvalidShipper);
        }

        if (_assignments.Any(assignment => assignment.IsActive))
        {
            return Result.Failure(ShipmentErrors.ActiveAssignmentExists);
        }

        _assignments.Add(new ShipmentAssignment(Id, shipperId));
        ChangeStatus(ShipmentStatus.Assigned, assignedByUserId, note ?? "Shipper assigned.");

        return Result.Success();
    }

    public Result UpdateStatus(ShipmentStatus newStatus, Guid changedByUserId, string? note = null)
    {
        if (IsTerminalStatus(Status))
        {
            return Result.Failure(ShipmentErrors.CompletedShipmentCannotChange);
        }

        if (newStatus == ShipmentStatus.DeliveryFailed && string.IsNullOrWhiteSpace(note))
        {
            return Result.Failure(ShipmentErrors.DeliveryFailedNoteRequired);
        }

        if (!CanTransitionTo(Status, newStatus))
        {
            return Result.Failure(ShipmentErrors.InvalidStatusTransition);
        }

        if (newStatus == ShipmentStatus.Returned)
        {
            ApplyReturnFee();
        }

        ChangeStatus(newStatus, changedByUserId, note);

        if (newStatus == ShipmentStatus.Returned
            || (newStatus == ShipmentStatus.Delivered && CodAmount.IsZero))
        {
            DeactivateActiveAssignments();
        }

        return Result.Success();
    }

    public Result Cancel(Guid cancelledByUserId, string reason)
    {
        if (Status is ShipmentStatus.PickedUp
            or ShipmentStatus.InTransit
            or ShipmentStatus.Delivering
            or ShipmentStatus.Delivered
            or ShipmentStatus.DeliveryFailed
            or ShipmentStatus.Returned
            or ShipmentStatus.Cancelled)
        {
            return Result.Failure(ShipmentErrors.CannotCancel);
        }

        DeactivateActiveAssignments();

        ChangeStatus(ShipmentStatus.Cancelled, cancelledByUserId, reason);

        return Result.Success();
    }

    public Result UpdateBeforePickup(
        string senderName,
        PhoneNumber senderPhone,
        string receiverName,
        PhoneNumber receiverPhone,
        Address pickupAddress,
        Address deliveryAddress,
        Weight weight,
        ParcelDimensions parcelDimensions,
        Weight chargeableWeight,
        Money goodsValue,
        Money codAmount,
        ShippingFeeBreakdown shippingFeeBreakdown,
        RouteType routeType,
        Guid changedByUserId,
        string? note = null)
    {
        if (Status is not (ShipmentStatus.Draft or ShipmentStatus.PendingPickup)
            || _assignments.Any(assignment => assignment.IsActive))
        {
            return Result.Failure(ShipmentErrors.CannotEditBeforePickup);
        }

        SenderName = RequireText(senderName, nameof(senderName));
        SenderPhone = senderPhone;
        ReceiverName = RequireText(receiverName, nameof(receiverName));
        ReceiverPhone = receiverPhone;
        PickupAddress = pickupAddress;
        DeliveryAddress = deliveryAddress;
        Weight = weight;
        ParcelDimensions = parcelDimensions;
        ChargeableWeight = chargeableWeight;
        GoodsValue = goodsValue;
        CodAmount = codAmount;
        ShippingFeeBreakdown = shippingFeeBreakdown;
        ShippingFee = shippingFeeBreakdown.TotalFee;
        RouteType = routeType;
        Note = note?.Trim();

        AddStatusHistory(Status, changedByUserId, "Shipment details updated before pickup.");
        MarkUpdated();

        return Result.Success();
    }

    public Result SubmitDraft(Guid submittedByUserId)
    {
        if (Status != ShipmentStatus.Draft)
        {
            return Result.Failure(ShipmentErrors.OnlyDraftCanBeSubmitted);
        }

        ChangeStatus(ShipmentStatus.PendingPickup, submittedByUserId, "Draft submitted.");

        return Result.Success();
    }

    public void DeactivateActiveAssignments()
    {
        var deactivatedAny = false;

        foreach (var assignment in _assignments.Where(assignment => assignment.IsActive))
        {
            assignment.Deactivate();
            deactivatedAny = true;
        }

        if (deactivatedAny)
        {
            MarkUpdated();
        }
    }

    private void ChangeStatus(ShipmentStatus newStatus, Guid changedByUserId, string? note)
    {
        Status = newStatus;
        AddStatusHistory(newStatus, changedByUserId, note);
        MarkUpdated();
    }

    private void ApplyReturnFee()
    {
        ShippingFeeBreakdown = ShippingFeeBreakdown.WithCalculatedReturnFee();
        ShippingFee = ShippingFeeBreakdown.TotalFee;
    }

    private void AddStatusHistory(ShipmentStatus status, Guid changedByUserId, string? note)
    {
        _statusHistory.Add(new ShipmentStatusHistory(Id, status, changedByUserId, note));
    }

    private static bool CanTransitionTo(ShipmentStatus currentStatus, ShipmentStatus newStatus)
    {
        return currentStatus switch
        {
            ShipmentStatus.Draft => newStatus is ShipmentStatus.PendingPickup or ShipmentStatus.Cancelled,
            ShipmentStatus.PendingPickup => newStatus is ShipmentStatus.Assigned or ShipmentStatus.Cancelled,
            ShipmentStatus.Assigned => newStatus is ShipmentStatus.PickingUp or ShipmentStatus.Cancelled,
            ShipmentStatus.PickingUp => newStatus is ShipmentStatus.PickedUp or ShipmentStatus.Cancelled,
            ShipmentStatus.PickedUp => newStatus is ShipmentStatus.InTransit or ShipmentStatus.Returned,
            ShipmentStatus.InTransit => newStatus is ShipmentStatus.Delivering or ShipmentStatus.Returned,
            ShipmentStatus.Delivering => newStatus is ShipmentStatus.Delivered or ShipmentStatus.DeliveryFailed or ShipmentStatus.Returned,
            ShipmentStatus.DeliveryFailed => newStatus is ShipmentStatus.Delivering or ShipmentStatus.Returned,
            _ => false
        };
    }

    private static bool IsTerminalStatus(ShipmentStatus status)
    {
        return status is ShipmentStatus.Delivered or ShipmentStatus.Returned or ShipmentStatus.Cancelled;
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
