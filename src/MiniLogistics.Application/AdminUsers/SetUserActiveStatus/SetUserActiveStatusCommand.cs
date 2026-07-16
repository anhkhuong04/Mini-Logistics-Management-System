namespace MiniLogistics.Application.AdminUsers.SetUserActiveStatus;

public sealed record SetUserActiveStatusCommand(
    Guid RequestedByUserId,
    Guid TargetUserId,
    bool IsActive,
    string? Reason = null);
