using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Operations;

public interface IOperationsReadService
{
    Task<Result<PagedResponse<GetOperationsShipmentResponse>>> ShipmentsAsync(Guid actor, GetOperationsShipmentsQuery query, CancellationToken cancellationToken);
    Task<Result<PagedResponse<GetPendingPickupShipmentResponse>>> PendingAsync(Guid actor, GetPendingPickupShipmentsQuery query, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<OperationsShipperResponse>>> ShippersAsync(Guid actor, CancellationToken cancellationToken);
    Task<Result<PagedResponse<OperationsCodCandidateResponse>>> SettlementAsync(Guid actor, int pageNumber, int pageSize, CancellationToken cancellationToken);
}
