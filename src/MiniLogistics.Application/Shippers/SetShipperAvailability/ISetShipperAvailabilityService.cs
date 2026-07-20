using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.SetShipperAvailability;

public interface ISetShipperAvailabilityService
{
    Task<Result> SetAsync(
        SetShipperAvailabilityCommand command,
        CancellationToken cancellationToken = default);
}
