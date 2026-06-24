namespace MiniLogistics.Application.AdminUsers.CreateInternalUser;

public sealed record CreateInternalUserResponse(
    Guid UserId,
    string FullName,
    string Email,
    string Role);
