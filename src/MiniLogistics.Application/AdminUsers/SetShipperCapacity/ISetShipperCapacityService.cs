using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.SetShipperCapacity;

/// <summary>
/// Defines the application use case contract for Set Shipper Capacity.
/// </summary>
public interface ISetShipperCapacityService
{
    Task<Result> SetAsync(
        SetShipperCapacityCommand command,
        CancellationToken cancellationToken = default);
}
