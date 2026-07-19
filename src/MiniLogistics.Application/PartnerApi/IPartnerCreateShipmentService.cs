using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Creates shipments submitted through authenticated Partner API clients.
/// </summary>
public interface IPartnerCreateShipmentService
{
    /// <summary>
    /// Creates a shipment or replays an existing idempotent create result.
    /// </summary>
    /// <param name="command">The authenticated partner create-shipment command.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerCreateShipmentResult>> CreateAsync(
        PartnerCreateShipmentCommand command,
        CancellationToken cancellationToken = default);
}
