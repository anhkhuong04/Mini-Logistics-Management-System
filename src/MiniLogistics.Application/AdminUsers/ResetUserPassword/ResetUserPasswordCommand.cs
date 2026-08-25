namespace MiniLogistics.Application.AdminUsers.ResetUserPassword;

public sealed record ResetUserPasswordCommand(
    Guid RequestedByUserId,
    Guid TargetUserId,
    string NewPassword,
    string? Reason = null);
