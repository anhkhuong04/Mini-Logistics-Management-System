using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;

public interface ICancelShipmentForCurrentShopService
{
    Task<Result> CancelAsync(
        CancelShipmentCommand command,
        CancellationToken cancellationToken = default);
}
