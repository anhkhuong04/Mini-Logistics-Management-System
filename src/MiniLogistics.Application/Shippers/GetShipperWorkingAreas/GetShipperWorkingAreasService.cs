using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shippers.GetShipperWorkingAreas;

public sealed class GetShipperWorkingAreasService : IGetShipperWorkingAreasService
{
    private readonly IIdentityService _identityService;
    private readonly IHubRepository _hubRepository;
    private readonly IShipperWorkingAreaRepository _workingAreaRepository;

    public GetShipperWorkingAreasService(
        IIdentityService identityService,
        IHubRepository hubRepository,
        IShipperWorkingAreaRepository workingAreaRepository)
    {
        _identityService = identityService;
        _hubRepository = hubRepository;
        _workingAreaRepository = workingAreaRepository;
    }

    public async Task<Result<IReadOnlyList<ShipperWorkingAreaResponse>>> GetAsync(
        Guid requestedByUserId,
        Guid shipperId,
        CancellationToken cancellationToken = default)
    {
        var shipperCheck = await _identityService.CheckUserRoleAsync(
            shipperId,
            nameof(UserRole.Shipper),
            cancellationToken);
        if (!shipperCheck.Exists)
        {
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(
                ApplicationErrors.NotFound("Shipper was not found."));
        }

        if (!shipperCheck.IsInRole)
        {
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(
                ApplicationErrors.Forbidden("Selected user is not a shipper."));
        }

        if (requestedByUserId != shipperId)
        {
            var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
                _identityService,
                requestedByUserId,
                cancellationToken);
            if (authorizationResult.IsFailure)
            {
                return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(authorizationResult.Error);
            }
        }
        else if (!shipperCheck.IsActive)
        {
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(
                ApplicationErrors.Forbidden("Shipper is not active."));
        }

        var workingAreas = await _workingAreaRepository.GetByShipperIdAsync(
            shipperId,
            activeOnly: true,
            cancellationToken);
        var hubs = await _hubRepository.GetByIdsAsync(
            workingAreas.Select(area => area.HubId).Distinct().ToList(),
            cancellationToken);

        return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Success(
            ShipperWorkingAreaResponseMapper.ToResponses(workingAreas, hubs.ToDictionary(hub => hub.Id)));
    }
}
