namespace MiniLogistics.Application.Fees;

/// <summary>
/// Defines cache operations for Fee Rule Cache.
/// </summary>
public interface IFeeRuleCache : IFeeRuleRepository
{
    void Invalidate();
}
