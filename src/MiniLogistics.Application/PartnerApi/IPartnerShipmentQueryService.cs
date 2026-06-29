using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

public interface IPartnerShipmentQueryService
{
    Task<Result<PartnerShipmentTrackingResponse>> GetAsync(
        PartnerGetShipmentCommand command,
        CancellationToken cancellationToken = default);
}
