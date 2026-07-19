using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.GetLogisticsHubs;

/// <summary>
/// Defines the application use case contract for Get Logistics Hubs.
/// </summary>
public interface IGetLogisticsHubsService
{
    Task<Result<IReadOnlyList<LogisticsHubResponse>>> GetAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default);
}
