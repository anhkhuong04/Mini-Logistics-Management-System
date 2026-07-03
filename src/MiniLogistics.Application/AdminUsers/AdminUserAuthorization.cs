using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers;

public static class AdminUserAuthorization
{
    public static async Task<Result> EnsureActiveAdminAsync(
        IIdentityService identityService,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var adminCheck = await identityService.CheckUserRoleAsync(
            adminUserId,
            nameof(UserRole.Admin),
            cancellationToken);

        if (!adminCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Admin user was not found."));
        }

        if (!adminCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Admin user is not active."));
        }

        return adminCheck.IsInRole
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden("Only Admin can manage internal users."));
    }
}
