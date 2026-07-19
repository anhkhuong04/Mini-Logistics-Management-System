using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Fees;

/// <summary>
/// Defines persistence operations for Fee Rule data.
/// </summary>
public interface IFeeRuleRepository
{
    Task<IReadOnlyCollection<FeeRule>> GetActiveRulesAsync(
        RouteType routeType,
        CancellationToken cancellationToken = default);
}
