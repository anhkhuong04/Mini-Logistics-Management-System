using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.CashOnDelivery.GetCodSettlementCandidates;

/// <summary>
/// Defines the application use case contract for Get Cod Settlement Candidates.
/// </summary>
public interface IGetCodSettlementCandidatesService
{
    Task<Result<IReadOnlyList<GetCodSettlementCandidateResponse>>> GetAsync(
        CancellationToken cancellationToken = default);
}
