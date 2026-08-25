using Microsoft.AspNetCore.Identity;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Infrastructure.Identity;

public sealed class UserPasswordService : IUserPasswordService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserPasswordService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result> ResetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound(userId));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded
            ? Result.Success()
            : Result.Failure(IdentityErrors.PasswordResetFailed(FormatErrors(result.Errors)));
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors) =>
        string.Join("; ", errors.Select(error => error.Description));
}
