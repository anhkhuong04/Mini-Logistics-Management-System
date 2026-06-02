namespace MiniLogistics.Application.Shipments;

public sealed record ShipmentAddressResponse(
    string Street,
    string Ward,
    string Province,
    string Country,
    string FullAddress);
