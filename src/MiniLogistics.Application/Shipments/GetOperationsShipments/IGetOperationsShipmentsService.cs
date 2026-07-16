using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetOperationsShipments;

public interface IGetOperationsShipmentsService
{
    Task<Result<IReadOnlyList<GetOperationsShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<GetOperationsShipmentResponse>>> SearchAsync(
        GetOperationsShipmentsQuery query,
        CancellationToken cancellationToken = default);
}
