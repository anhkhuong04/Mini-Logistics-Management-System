using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.DraftShipments;

public interface IUpdateShipmentBeforePickupService
{
    Task<Result<DraftShipmentResponse>> UpdateAsync(
        UpdateShipmentBeforePickupCommand command,
        CancellationToken cancellationToken = default);
}
