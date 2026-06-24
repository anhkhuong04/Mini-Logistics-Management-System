using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.GetAdminUsers;

public interface IGetAdminUsersService
{
    Task<Result<IReadOnlyList<GetAdminUserResponse>>> GetAsync(
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);
}
