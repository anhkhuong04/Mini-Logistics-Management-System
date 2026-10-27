using MiniLogistics.Application.Common;

namespace MiniLogistics.Application.Operations;

public interface IOperationsSettlementReadRepository
{
    Task<PagedResponse<OperationsCodCandidateResponse>> SearchAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
}
