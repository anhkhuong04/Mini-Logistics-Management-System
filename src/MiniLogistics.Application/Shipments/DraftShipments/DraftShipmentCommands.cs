using MiniLogistics.Application.Shipments.CreateShipment;

namespace MiniLogistics.Application.Shipments.DraftShipments;

/// <summary>
/// Defines the application contract for Shipment Details Command.
/// </summary>
public interface IShipmentDetailsCommand
{
    Guid UserId { get; }

    string SenderName { get; }

    string SenderPhone { get; }

    string ReceiverName { get; }

    string ReceiverPhone { get; }

    ShipmentAddressDto PickupAddress { get; }

    ShipmentAddressDto DeliveryAddress { get; }

    decimal WeightKg { get; }

    decimal LengthCm { get; }

    decimal WidthCm { get; }

    decimal HeightCm { get; }

    decimal GoodsValueAmount { get; }

    decimal CodAmount { get; }

    string Currency { get; }

    string? Note { get; }

    Guid? ShopId { get; }
}

public sealed record CreateDraftShipmentCommand(
    Guid UserId,
    string SenderName,
    string SenderPhone,
    string ReceiverName,
    string ReceiverPhone,
    ShipmentAddressDto PickupAddress,
    ShipmentAddressDto DeliveryAddress,
    decimal WeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal GoodsValueAmount,
    decimal CodAmount,
    string Currency = "VND",
    string? Note = null,
    Guid? ShopId = null) : IShipmentDetailsCommand;

public sealed record UpdateShipmentBeforePickupCommand(
    Guid UserId,
    Guid ShipmentId,
    string SenderName,
    string SenderPhone,
    string ReceiverName,
    string ReceiverPhone,
    ShipmentAddressDto PickupAddress,
    ShipmentAddressDto DeliveryAddress,
    decimal WeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal GoodsValueAmount,
    decimal CodAmount,
    string Currency = "VND",
    string? Note = null,
    Guid? ShopId = null) : IShipmentDetailsCommand;

public sealed record SubmitDraftShipmentCommand(
    Guid UserId,
    Guid ShipmentId,
    Guid? ShopId = null);
