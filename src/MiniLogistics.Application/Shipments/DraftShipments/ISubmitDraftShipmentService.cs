using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.DraftShipments;

/// <summary>
/// Defines the application use case contract for Submit Draft Shipment.
/// </summary>
public interface ISubmitDraftShipmentService
{
    Task<Result<DraftShipmentResponse>> SubmitAsync(
        SubmitDraftShipmentCommand command,
        CancellationToken cancellationToken = default);
}
