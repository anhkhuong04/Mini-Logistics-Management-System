namespace MiniLogistics.Application.AdminUsers.CreateInternalUser;

public sealed record CreateInternalUserCommand(
    Guid RequestedByUserId,
    string FullName,
    string Email,
    string PhoneNumber,
    string Password,
    string Role);
