using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.SetShipperWorkingAreas;

public interface ISetShipperWorkingAreasService
{
    Task<Result<IReadOnlyList<ShipperWorkingAreaResponse>>> SetAsync(
        SetShipperWorkingAreasCommand command,
        CancellationToken cancellationToken = default);
}
