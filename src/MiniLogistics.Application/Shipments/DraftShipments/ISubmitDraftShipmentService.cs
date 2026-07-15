using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.DraftShipments;

public interface ISubmitDraftShipmentService
{
    Task<Result<DraftShipmentResponse>> SubmitAsync(
        SubmitDraftShipmentCommand command,
        CancellationToken cancellationToken = default);
}
