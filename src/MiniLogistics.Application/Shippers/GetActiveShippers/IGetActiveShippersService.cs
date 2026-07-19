using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.GetActiveShippers;

/// <summary>
/// Defines the application use case contract for Get Active Shippers.
/// </summary>
public interface IGetActiveShippersService
{
    Task<Result<IReadOnlyList<GetActiveShipperResponse>>> GetAsync(
        CancellationToken cancellationToken = default);
}
