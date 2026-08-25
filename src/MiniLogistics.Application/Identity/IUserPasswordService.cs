using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Identity;

/// <summary>
/// Changes a user's password through the configured identity provider.
/// Implementations must never log or persist the supplied raw password.
/// </summary>
public interface IUserPasswordService
{
    Task<Result> ResetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default);
}
