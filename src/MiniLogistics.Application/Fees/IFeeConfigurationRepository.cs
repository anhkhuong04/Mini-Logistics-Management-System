using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Fees;

public interface IFeeConfigurationRepository
{
    Task<IReadOnlyList<FeeRule>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeeRule>> GetActiveRulesForUpdateAsync(
        RouteType routeType,
        CancellationToken cancellationToken = default);

    Task<int> GetLatestVersionAsync(
        RouteType routeType,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        FeeRule feeRule,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
