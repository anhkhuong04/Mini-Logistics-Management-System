using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetPendingPickupShipments;

public interface IGetPendingPickupShipmentsService
{
    Task<Result<IReadOnlyList<GetPendingPickupShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default);
}
