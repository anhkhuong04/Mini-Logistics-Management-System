using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.CashOnDelivery.GetCodSettlementCandidates;

public interface IGetCodSettlementCandidatesService
{
    Task<Result<IReadOnlyList<GetCodSettlementCandidateResponse>>> GetAsync(
        CancellationToken cancellationToken = default);
}
