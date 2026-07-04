using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.GetActiveShippers;

public sealed class GetActiveShippersService : IGetActiveShippersService
{
    private readonly IIdentityService _identityService;
    private readonly IHubRepository _hubRepository;
    private readonly IShipperWorkingAreaRepository _workingAreaRepository;

    public GetActiveShippersService(
        IIdentityService identityService,
        IHubRepository hubRepository,
        IShipperWorkingAreaRepository workingAreaRepository)
    {
        _identityService = identityService;
        _hubRepository = hubRepository;
        _workingAreaRepository = workingAreaRepository;
    }

    public async Task<Result<IReadOnlyList<GetActiveShipperResponse>>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var shippers = await _identityService.GetActiveShippersAsync(cancellationToken);
        var shipperIds = shippers.Select(shipper => shipper.UserId).ToList();
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

        var response = shippers
            .Select(shipper => new GetActiveShipperResponse(
                shipper.UserId,
                shipper.FullName,
                shipper.Email,
                shipper.PhoneNumber,
                shipper.IsAvailableForAssignment,
                shipper.MaxActiveShipments,
                areasByShipperId.TryGetValue(shipper.UserId, out var areas) ? areas : []))
            .ToList();

        return Result<IReadOnlyList<GetActiveShipperResponse>>.Success(response);
    }
}
