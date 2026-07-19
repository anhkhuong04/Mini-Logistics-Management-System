namespace MiniLogistics.Web.Endpoints;

public sealed record PartnerQuoteRequest(
    string? ExternalOrderId,
    PartnerAddressRequest? PickupAddress,
    PartnerAddressRequest? DeliveryAddress,
    PartnerParcelRequest? Parcel,
    decimal GoodsValueAmount,
    decimal CodAmount,
    string? Currency);

public sealed record PartnerCreateShipmentRequest(
    string? ExternalOrderId,
    PartnerPartyRequest? Sender,
    PartnerPartyRequest? Receiver,
    PartnerAddressRequest? PickupAddress,
    PartnerAddressRequest? DeliveryAddress,
    PartnerParcelRequest? Parcel,
    decimal GoodsValueAmount,
    decimal CodAmount,
    string? Currency,
    string? Note);

public sealed record PartnerCancelShipmentRequest(
    string? Reason);

public sealed record PartnerPartyRequest(
    string? Name,
    string? Phone);

public sealed record PartnerAddressRequest(
    string? Street,
    string? Ward,
    string? Province,
    string? Country);

public sealed record PartnerParcelRequest(
    decimal WeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm);
