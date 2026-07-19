using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.GetAdminUsers;

/// <summary>
/// Defines the application use case contract for Get Admin Users.
/// </summary>
public interface IGetAdminUsersService
{
    Task<Result<IReadOnlyList<GetAdminUserResponse>>> GetAsync(
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<GetAdminUserResponse>>> SearchAsync(
        GetAdminUsersQuery query,
        CancellationToken cancellationToken = default);
}
