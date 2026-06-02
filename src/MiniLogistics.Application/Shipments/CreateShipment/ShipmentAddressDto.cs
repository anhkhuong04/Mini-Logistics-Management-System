namespace MiniLogistics.Application.Shipments.CreateShipment;

public sealed record ShipmentAddressDto(
    string Street,
    string Ward,
    string Province,
    string Country = "Vietnam");
