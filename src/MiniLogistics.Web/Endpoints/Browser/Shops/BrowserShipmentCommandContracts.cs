using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shipments.DraftShipments;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserShipmentWriteRequest(string SenderName, string SenderPhone, string ReceiverName, string ReceiverPhone,
    ShipmentAddressDto PickupAddress, ShipmentAddressDto DeliveryAddress, decimal WeightKg, decimal LengthCm, decimal WidthCm,
    decimal HeightCm, decimal GoodsValueAmount, decimal CodAmount, string Currency = "VND", string? Note = null)
{
    public CreateShipmentCommand Create(Guid actor, Guid shop) => new(actor, SenderName, SenderPhone, ReceiverName, ReceiverPhone,
        PickupAddress, DeliveryAddress, WeightKg, LengthCm, WidthCm, HeightCm, GoodsValueAmount, CodAmount, Currency, Note, shop);
    public CreateDraftShipmentCommand Draft(Guid actor, Guid shop) => new(actor, SenderName, SenderPhone, ReceiverName, ReceiverPhone,
        PickupAddress, DeliveryAddress, WeightKg, LengthCm, WidthCm, HeightCm, GoodsValueAmount, CodAmount, Currency, Note, shop);
    public UpdateShipmentBeforePickupCommand Update(Guid actor, Guid shop, Guid shipment) => new(actor, shipment, SenderName, SenderPhone, ReceiverName, ReceiverPhone,
        PickupAddress, DeliveryAddress, WeightKg, LengthCm, WidthCm, HeightCm, GoodsValueAmount, CodAmount, Currency, Note, shop);
}

public sealed record BrowserShipmentCancelRequest(string Reason);
