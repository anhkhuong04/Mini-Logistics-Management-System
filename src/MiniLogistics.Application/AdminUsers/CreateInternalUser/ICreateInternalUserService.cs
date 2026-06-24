using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.CreateInternalUser;

public interface ICreateInternalUserService
{
    Task<Result<CreateInternalUserResponse>> CreateAsync(
        CreateInternalUserCommand command,
        CancellationToken cancellationToken = default);
}
