using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Identity;

public interface IIdentityService
{
    Task<Result<Guid>> CreateUserAsync(
        string fullName,
        string email,
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result> AddToRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);
}
