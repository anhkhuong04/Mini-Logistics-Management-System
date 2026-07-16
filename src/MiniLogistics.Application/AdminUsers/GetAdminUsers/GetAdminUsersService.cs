using MiniLogistics.Application.Common;
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
        var result = await SearchAsync(
            new GetAdminUsersQuery(requestedByUserId, PageNumber: 1, PageSize: int.MaxValue),
            cancellationToken);

        return result.IsSuccess
            ? Result<IReadOnlyList<GetAdminUserResponse>>.Success(result.Value.Items)
            : Result<IReadOnlyList<GetAdminUserResponse>>.Failure(result.Error);
    }

    public async Task<Result<PagedResponse<GetAdminUserResponse>>> SearchAsync(
        GetAdminUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            query.RequestedByUserId,
            cancellationToken);

        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<GetAdminUserResponse>>.Failure(authorizationResult.Error);
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
            .Where(user => Matches(query, user))
            .Select(user => new GetAdminUserResponse(
                user.UserId,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.IsActive,
                user.IsAvailableForAssignment,
                user.MaxActiveShipments,
                user.Roles,
                user.CreatedAtUtc,
                areasByShipperId.TryGetValue(user.UserId, out var areas) ? areas : []))
            .ToList();

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = query.PageSize == int.MaxValue
            ? int.MaxValue
            : Math.Clamp(query.PageSize, 1, 100);
        var items = response
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result<PagedResponse<GetAdminUserResponse>>.Success(
            new PagedResponse<GetAdminUserResponse>(
                items,
                pageNumber,
                pageSize,
                response.Count));
    }

    private static bool Matches(GetAdminUsersQuery query, IdentityUserWithRolesResponse user)
    {
        if (!string.IsNullOrWhiteSpace(query.Role)
            && !user.Roles.Contains(query.Role.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.IsActive.HasValue && user.IsActive != query.IsActive.Value)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.SearchText))
        {
            return true;
        }

        var keyword = query.SearchText.Trim();
        return user.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || user.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || (user.PhoneNumber?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            || user.UserId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
