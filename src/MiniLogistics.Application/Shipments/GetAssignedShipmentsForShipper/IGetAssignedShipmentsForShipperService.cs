using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;

/// <summary>
/// Defines the application use case contract for Get Assigned Shipments For Shipper.
/// </summary>
public interface IGetAssignedShipmentsForShipperService
{
    Task<Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>> GetAsync(
        Guid shipperUserId,
        CancellationToken cancellationToken = default);
}
