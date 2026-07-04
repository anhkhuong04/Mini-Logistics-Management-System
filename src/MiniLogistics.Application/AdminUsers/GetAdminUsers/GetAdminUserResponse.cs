using MiniLogistics.Application.Shippers;

namespace MiniLogistics.Application.AdminUsers.GetAdminUsers;

public sealed record GetAdminUserResponse(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsAvailableForAssignment,
    int MaxActiveShipments,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ShipperWorkingAreaResponse> WorkingAreas);
