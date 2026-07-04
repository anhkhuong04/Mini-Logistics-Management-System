using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.SetShipperCapacity;

public interface ISetShipperCapacityService
{
    Task<Result> SetAsync(
        SetShipperCapacityCommand command,
        CancellationToken cancellationToken = default);
}
