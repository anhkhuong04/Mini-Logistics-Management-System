using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.DraftShipments;

public interface ICreateDraftShipmentService
{
    Task<Result<DraftShipmentResponse>> CreateAsync(
        CreateDraftShipmentCommand command,
        CancellationToken cancellationToken = default);
}
