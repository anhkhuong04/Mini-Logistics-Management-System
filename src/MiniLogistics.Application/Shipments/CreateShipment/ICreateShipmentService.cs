using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.CreateShipment;

/// <summary>
/// Defines the application use case contract for Create Shipment.
/// </summary>
public interface ICreateShipmentService
{
    Task<Result<CreateShipmentResponse>> CreateAsync(
        CreateShipmentCommand command,
        CancellationToken cancellationToken = default);
}
