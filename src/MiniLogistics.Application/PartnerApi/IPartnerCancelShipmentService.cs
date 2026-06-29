using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

public interface IPartnerCancelShipmentService
{
    Task<Result<PartnerShipmentTrackingResponse>> CancelAsync(
        PartnerCancelShipmentCommand command,
        CancellationToken cancellationToken = default);
}
