using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Fees;

public interface IFeeRuleRepository
{
    Task<IReadOnlyCollection<FeeRule>> GetActiveRulesAsync(
        RouteType routeType,
        CancellationToken cancellationToken = default);
}
