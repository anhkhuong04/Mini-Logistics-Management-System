namespace MiniLogistics.Application.Fees;

/// <summary>
/// Defines cache operations for Fee Rule Cache.
/// </summary>
public interface IFeeRuleCache : IFeeRuleRepository
{
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
