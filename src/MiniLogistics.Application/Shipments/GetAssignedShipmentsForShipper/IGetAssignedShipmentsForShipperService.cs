using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;

public interface IGetAssignedShipmentsForShipperService
{
    Task<Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>> GetAsync(
        Guid shipperUserId,
        CancellationToken cancellationToken = default);
}
