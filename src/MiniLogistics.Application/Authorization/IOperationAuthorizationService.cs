using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Authorization;

public interface IOperationAuthorizationService
{
    Task<Result> EnsurePermissionAsync(
        Guid actorUserId,
        string permission,
        string notFoundMessage,
        string inactiveMessage,
        string forbiddenMessage,
        CancellationToken cancellationToken = default);
}
