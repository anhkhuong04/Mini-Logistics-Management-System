using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.DraftShipments;

/// <summary>
/// Defines the application use case contract for Create Draft Shipment.
/// </summary>
public interface ICreateDraftShipmentService
{
    Task<Result<DraftShipmentResponse>> CreateAsync(
        CreateDraftShipmentCommand command,
        CancellationToken cancellationToken = default);
}
