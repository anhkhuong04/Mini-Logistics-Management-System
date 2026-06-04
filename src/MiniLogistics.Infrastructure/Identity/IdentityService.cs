using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<Guid>> CreateUserAsync(
        string fullName,
        string email,
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email.Trim(),
            Email = email.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            FullName = fullName.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return Result<Guid>.Failure(IdentityErrors.UserCreationFailed(FormatErrors(result.Errors)));
        }

        return Result<Guid>.Success(user.Id);
    }

    public async Task<Result> AddToRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound(userId));
        }

        var result = await _userManager.AddToRoleAsync(user, role);
        return result.Succeeded
            ? Result.Success()
            : Result.Failure(IdentityErrors.RoleAssignmentFailed(FormatErrors(result.Errors)));
    }

    public async Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return new IdentityUserRoleCheckResponse(userId, false, false, false);
        }

        var isInRole = await _userManager.IsInRoleAsync(user, role);

        return new IdentityUserRoleCheckResponse(
            user.Id,
            true,
            user.IsActive,
            isInRole);
    }

    public async Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var shippers = await _userManager.GetUsersInRoleAsync("Shipper");

        return shippers
            .Where(shipper => shipper.IsActive)
            .OrderBy(shipper => shipper.FullName)
            .Select(shipper => new ActiveShipperResponse(
                shipper.Id,
                shipper.FullName,
                shipper.Email ?? string.Empty,
                shipper.PhoneNumber))
            .ToList();
    }

    public async Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await _userManager.Users
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new IdentityUserSummaryResponse(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                user.PhoneNumber,
                user.IsActive))
            .ToListAsync(cancellationToken);
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors)
    {
        return string.Join("; ", errors.Select(error => error.Description));
    }
}
