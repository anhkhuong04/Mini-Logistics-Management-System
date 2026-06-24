using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.GetAdminUsers;

public sealed class GetAdminUsersService : IGetAdminUsersService
{
    private readonly IIdentityService _identityService;

    public GetAdminUsersService(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result<IReadOnlyList<GetAdminUserResponse>>> GetAsync(
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            requestedByUserId,
            cancellationToken);

        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyList<GetAdminUserResponse>>.Failure(authorizationResult.Error);
        }

        var users = await _identityService.ListUsersWithRolesAsync(cancellationToken);
        var response = users
            .Select(user => new GetAdminUserResponse(
                user.UserId,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.IsActive,
                user.Roles,
                user.CreatedAtUtc))
            .ToList();

        return Result<IReadOnlyList<GetAdminUserResponse>>.Success(response);
    }
}
