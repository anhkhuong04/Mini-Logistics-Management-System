using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

public interface IPartnerCreateShipmentService
{
    Task<Result<PartnerCreateShipmentResult>> CreateAsync(
        PartnerCreateShipmentCommand command,
        CancellationToken cancellationToken = default);
}
