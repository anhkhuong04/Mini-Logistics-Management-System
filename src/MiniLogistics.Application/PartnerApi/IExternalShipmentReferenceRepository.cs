using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Defines persistence operations for External Shipment Reference data.
/// </summary>
public interface IExternalShipmentReferenceRepository
{
    Task<ExternalShipmentReference?> GetByApiClientAndIdempotencyKeyAsync(
        Guid apiClientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExternalShipmentReference?> GetByApiClientAndExternalOrderIdAsync(
        Guid apiClientId,
        string externalOrderId,
        CancellationToken cancellationToken = default);

    Task<ExternalShipmentReference?> GetByApiClientAndShipmentIdAsync(
        Guid apiClientId,
        Guid shipmentId,
        CancellationToken cancellationToken = default);

    Task<ExternalShipmentReference?> GetByShipmentIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ExternalShipmentReference reference,
        CancellationToken cancellationToken = default);
}
