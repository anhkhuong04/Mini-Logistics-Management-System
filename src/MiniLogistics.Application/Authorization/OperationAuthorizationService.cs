using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Authorization;

public sealed class OperationAuthorizationService : IOperationAuthorizationService
{
    private readonly IIdentityService _identityService;

    public OperationAuthorizationService(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> EnsurePermissionAsync(
        Guid actorUserId,
        string permission,
        string notFoundMessage,
        string inactiveMessage,
        string forbiddenMessage,
        CancellationToken cancellationToken = default)
    {
        foreach (var role in new[] { nameof(UserRole.Admin), nameof(UserRole.Operator) })
        {
            var roleCheck = await _identityService.CheckUserRoleAsync(actorUserId, role, cancellationToken);
            if (!roleCheck.Exists)
            {
                return Result.Failure(ApplicationErrors.NotFound(notFoundMessage));
            }

            if (!roleCheck.IsActive)
            {
                return Result.Failure(ApplicationErrors.Forbidden(inactiveMessage));
            }

            if (roleCheck.IsInRole && OperationPermissions.ForRole(role).Contains(permission))
            {
                return Result.Success();
            }
        }

        return Result.Failure(ApplicationErrors.Forbidden(forbiddenMessage));
    }
}
