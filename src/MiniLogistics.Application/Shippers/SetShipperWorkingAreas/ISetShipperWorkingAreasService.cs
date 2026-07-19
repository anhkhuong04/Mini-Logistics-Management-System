using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.SetShipperWorkingAreas;

/// <summary>
/// Defines the application use case contract for Set Shipper Working Areas.
/// </summary>
public interface ISetShipperWorkingAreasService
{
    Task<Result<IReadOnlyList<ShipperWorkingAreaResponse>>> SetAsync(
        SetShipperWorkingAreasCommand command,
        CancellationToken cancellationToken = default);
}
