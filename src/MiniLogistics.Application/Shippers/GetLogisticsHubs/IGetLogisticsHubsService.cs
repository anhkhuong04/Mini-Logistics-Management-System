using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.GetLogisticsHubs;

public interface IGetLogisticsHubsService
{
    Task<Result<IReadOnlyList<LogisticsHubResponse>>> GetAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default);
}
