using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.ImportShipments;

public interface IPreviewShipmentImportService
{
    Task<Result<ShipmentImportPreviewResponse>> PreviewAsync(
        PreviewShipmentImportCommand command,
        CancellationToken cancellationToken = default);
}

public interface IConfirmShipmentImportService
{
    Task<Result<ShipmentImportConfirmResponse>> ConfirmAsync(
        ConfirmShipmentImportCommand command,
        CancellationToken cancellationToken = default);
}
