using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.CreateInternalUser;

/// <summary>
/// Defines the application use case contract for Create Internal User.
/// </summary>
public interface ICreateInternalUserService
{
    Task<Result<CreateInternalUserResponse>> CreateAsync(
        CreateInternalUserCommand command,
        CancellationToken cancellationToken = default);
}
