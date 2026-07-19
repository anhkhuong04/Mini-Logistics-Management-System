using MiniLogistics.Domain.Common;
using MiniLogistics.Application.Common;

namespace MiniLogistics.Application.Shipments.GetPendingPickupShipments;

/// <summary>
/// Defines the application use case contract for Get Pending Pickup Shipments.
/// </summary>
public interface IGetPendingPickupShipmentsService
{
    Task<Result<IReadOnlyList<GetPendingPickupShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<GetPendingPickupShipmentResponse>>> SearchAsync(
        GetPendingPickupShipmentsQuery query,
        CancellationToken cancellationToken = default);
}
