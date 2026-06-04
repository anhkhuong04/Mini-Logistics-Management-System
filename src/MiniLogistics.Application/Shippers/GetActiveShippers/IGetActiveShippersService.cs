using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.GetActiveShippers;

public interface IGetActiveShippersService
{
    Task<Result<IReadOnlyList<GetActiveShipperResponse>>> GetAsync(
        CancellationToken cancellationToken = default);
}
