using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.GetShipperWorkingAreas;

/// <summary>
/// Defines the application use case contract for Get Shipper Working Areas.
/// </summary>
public interface IGetShipperWorkingAreasService
{
    Task<Result<IReadOnlyList<ShipperWorkingAreaResponse>>> GetAsync(
        Guid requestedByUserId,
        Guid shipperId,
        CancellationToken cancellationToken = default);
}
