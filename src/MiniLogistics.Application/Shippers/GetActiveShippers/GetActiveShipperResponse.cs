using MiniLogistics.Application.Shippers;

namespace MiniLogistics.Application.Shippers.GetActiveShippers;

public sealed record GetActiveShipperResponse(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsAvailableForAssignment,
    int MaxActiveShipments,
    IReadOnlyList<ShipperWorkingAreaResponse> WorkingAreas);
