using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.GetShipperWorkingAreas;

public interface IGetShipperWorkingAreasService
{
    Task<Result<IReadOnlyList<ShipperWorkingAreaResponse>>> GetAsync(
        Guid requestedByUserId,
        Guid shipperId,
        CancellationToken cancellationToken = default);
}
