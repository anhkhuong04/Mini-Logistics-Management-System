namespace MiniLogistics.Application.Shippers.SetShipperWorkingAreas;

public sealed record SetShipperWorkingAreasCommand(
    Guid RequestedByUserId,
    Guid ShipperId,
    IReadOnlyList<SetShipperWorkingAreaItem> Areas);

public sealed record SetShipperWorkingAreaItem(
    Guid HubId,
    string? Ward = null,
    string? ZoneCode = null);
