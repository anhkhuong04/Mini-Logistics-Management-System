using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Reads shipment tracking data through authenticated Partner API clients.
/// </summary>
public interface IPartnerShipmentQueryService
{
    /// <summary>
    /// Returns tracking data for a partner-owned shipment.
    /// </summary>
    /// <param name="command">The authenticated tracking query command.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerShipmentTrackingResponse>> GetAsync(
        PartnerGetShipmentCommand command,
        CancellationToken cancellationToken = default);
}
