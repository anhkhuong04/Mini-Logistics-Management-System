using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.GetLogisticsHubs;

public sealed class GetLogisticsHubsService : IGetLogisticsHubsService
{
    private readonly IHubRepository _hubRepository;

    public GetLogisticsHubsService(IHubRepository hubRepository)
    {
        _hubRepository = hubRepository;
    }

    public async Task<Result<IReadOnlyList<LogisticsHubResponse>>> GetAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var hubs = await _hubRepository.GetAllAsync(activeOnly, cancellationToken);
        var response = hubs
            .Select(hub => new LogisticsHubResponse(
                hub.Id,
                hub.Code,
                hub.Name,
                hub.Province,
                hub.Ward,
                hub.AddressLine,
                hub.Country,
                hub.IsRegionalSortingHub,
                hub.IsActive))
            .ToList();

        return Result<IReadOnlyList<LogisticsHubResponse>>.Success(response);
    }
}
