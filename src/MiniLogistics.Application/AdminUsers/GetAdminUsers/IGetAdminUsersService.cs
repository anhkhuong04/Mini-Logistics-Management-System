using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.GetAdminUsers;

public interface IGetAdminUsersService
{
    Task<Result<IReadOnlyList<GetAdminUserResponse>>> GetAsync(
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<GetAdminUserResponse>>> SearchAsync(
        GetAdminUsersQuery query,
        CancellationToken cancellationToken = default);
}
