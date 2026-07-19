using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetOperationsShipments;

/// <summary>
/// Defines the application use case contract for Get Operations Shipments.
/// </summary>
public interface IGetOperationsShipmentsService
{
    Task<Result<IReadOnlyList<GetOperationsShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<GetOperationsShipmentResponse>>> SearchAsync(
        GetOperationsShipmentsQuery query,
        CancellationToken cancellationToken = default);
}
