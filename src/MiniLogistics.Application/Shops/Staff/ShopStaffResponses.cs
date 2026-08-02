using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.Staff;

public sealed record ShopStaffResponse(
    Guid MembershipId,
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    ShopStaffRole Role,
    ShopPermission Permissions,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
