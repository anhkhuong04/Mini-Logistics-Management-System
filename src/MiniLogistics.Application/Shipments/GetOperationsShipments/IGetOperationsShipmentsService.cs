using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetOperationsShipments;

public interface IGetOperationsShipmentsService
{
    Task<Result<IReadOnlyList<GetOperationsShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default);
}
