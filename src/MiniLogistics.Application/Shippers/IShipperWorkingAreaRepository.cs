using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Application.Shippers;

/// <summary>
/// Defines persistence operations for Shipper Working Area data.
/// </summary>
public interface IShipperWorkingAreaRepository
{
    Task<IReadOnlyList<ShipperWorkingArea>> GetByShipperIdAsync(
        Guid shipperId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShipperWorkingArea>> GetActiveByShipperIdsAsync(
        IReadOnlyCollection<Guid> shipperIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShipperWorkingArea>> GetActiveByHubOrProvinceAsync(
        Guid? hubId,
        string province,
        string? ward = null,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveByHubIdAsync(
        Guid hubId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ShipperWorkingArea workingArea,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
