namespace MiniLogistics.Application.Shippers;

public sealed record ShipperWorkingAreaResponse(
    Guid WorkingAreaId,
    Guid ShipperId,
    Guid HubId,
    string HubCode,
    string HubName,
    string Province,
    string? Ward,
    string? ZoneCode,
    bool IsActive);
