using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers.GetAdminUsers;

public sealed class GetAdminUsersService : IGetAdminUsersService
{
    private readonly IIdentityService _identityService;
    private readonly IHubRepository _hubRepository;
    private readonly IShipperWorkingAreaRepository _workingAreaRepository;

    public GetAdminUsersService(
        IIdentityService identityService,
        IHubRepository hubRepository,
        IShipperWorkingAreaRepository workingAreaRepository)
    {
        _identityService = identityService;
        _hubRepository = hubRepository;
        _workingAreaRepository = workingAreaRepository;
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
        var shipperIds = users
            .Where(user => user.Roles.Contains(nameof(UserRole.Shipper), StringComparer.OrdinalIgnoreCase))
            .Select(user => user.UserId)
            .ToList();
        var workingAreas = await _workingAreaRepository.GetActiveByShipperIdsAsync(
            shipperIds,
            cancellationToken);
        var hubs = await _hubRepository.GetByIdsAsync(
            workingAreas.Select(area => area.HubId).Distinct().ToList(),
            cancellationToken);
        var hubById = hubs.ToDictionary(hub => hub.Id);
        var areasByShipperId = workingAreas
            .GroupBy(area => area.ShipperId)
            .ToDictionary(
                group => group.Key,
                group => ShipperWorkingAreaResponseMapper.ToResponses(group, hubById));

        var response = users
            .Select(user => new GetAdminUserResponse(
                user.UserId,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.IsActive,
                user.Roles,
                user.CreatedAtUtc,
                areasByShipperId.TryGetValue(user.UserId, out var areas) ? areas : []))
            .ToList();

        return Result<IReadOnlyList<GetAdminUserResponse>>.Success(response);
    }
}
