using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Cancels eligible shipments through authenticated Partner API clients.
/// </summary>
public interface IPartnerCancelShipmentService
{
    /// <summary>
    /// Cancels a partner-owned shipment when its current state allows cancellation.
    /// </summary>
    /// <param name="command">The authenticated cancel-shipment command.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerShipmentTrackingResponse>> CancelAsync(
        PartnerCancelShipmentCommand command,
        CancellationToken cancellationToken = default);
}
