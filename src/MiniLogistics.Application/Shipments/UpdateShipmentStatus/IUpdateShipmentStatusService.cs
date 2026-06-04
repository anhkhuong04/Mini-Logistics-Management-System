using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.UpdateShipmentStatus;

public interface IUpdateShipmentStatusService
{
    Task<Result> UpdateAsync(
        UpdateShipmentStatusCommand command,
        CancellationToken cancellationToken = default);
}
