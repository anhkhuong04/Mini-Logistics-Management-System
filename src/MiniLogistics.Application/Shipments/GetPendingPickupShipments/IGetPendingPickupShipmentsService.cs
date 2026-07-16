using MiniLogistics.Domain.Common;
using MiniLogistics.Application.Common;

namespace MiniLogistics.Application.Shipments.GetPendingPickupShipments;

public interface IGetPendingPickupShipmentsService
{
    Task<Result<IReadOnlyList<GetPendingPickupShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<GetPendingPickupShipmentResponse>>> SearchAsync(
        GetPendingPickupShipmentsQuery query,
        CancellationToken cancellationToken = default);
}
