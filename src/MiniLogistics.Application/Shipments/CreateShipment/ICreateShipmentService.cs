using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.CreateShipment;

public interface ICreateShipmentService
{
    Task<Result<CreateShipmentResponse>> CreateAsync(
        CreateShipmentCommand command,
        CancellationToken cancellationToken = default);
}
