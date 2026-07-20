using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GenerateShipmentLabel;

public interface IGenerateShipmentLabelService
{
    Task<Result<ShipmentLabelResponse>> GenerateAsync(
        GenerateShipmentLabelCommand command,
        CancellationToken cancellationToken = default);
}
