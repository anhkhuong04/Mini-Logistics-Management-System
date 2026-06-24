using MiniLogistics.Domain.Common;

namespace MiniLogistics.Infrastructure.Identity;

public static class IdentityErrors
{
    public static Error UserCreationFailed(string description) =>
        new("Identity.UserCreationFailed", description);

    public static Error UserNotFound(Guid userId) =>
        new("Identity.UserNotFound", $"User '{userId}' was not found.");

    public static Error UserUpdateFailed(string description) =>
        new("Identity.UserUpdateFailed", description);

    public static Error RoleAssignmentFailed(string description) =>
        new("Identity.RoleAssignmentFailed", description);
}
