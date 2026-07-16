namespace MiniLogistics.Application.AdminUsers.GetAdminUsers;

public sealed record GetAdminUsersQuery(
    Guid RequestedByUserId,
    string? SearchText = null,
    string? Role = null,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 25);
