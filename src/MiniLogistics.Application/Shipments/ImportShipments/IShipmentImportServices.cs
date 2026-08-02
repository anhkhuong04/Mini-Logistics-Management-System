using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.ImportShipments;

/// <summary>
/// Defines the application use case contract for Preview Shipment Import.
/// </summary>
public interface IPreviewShipmentImportService
{
    Task<Result<ShipmentImportPreviewResponse>> PreviewAsync(
        PreviewShipmentImportCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the application use case contract for Confirm Shipment Import.
/// </summary>
public interface IConfirmShipmentImportService
{
    Task<Result<ShipmentImportConfirmResponse>> ConfirmAsync(
        ConfirmShipmentImportCommand command,
        CancellationToken cancellationToken = default);
}

public interface IGetShipmentImportBatchService
{
    Task<Result<ShipmentImportConfirmResponse>> GetAsync(
        Guid currentUserId,
        Guid? shopId,
        Guid batchId,
        CancellationToken cancellationToken = default);
}

public interface IExportShipmentImportErrorsService
{
    Task<Result<string>> ExportCsvAsync(
        Guid currentUserId,
        Guid? shopId,
        Guid batchId,
        CancellationToken cancellationToken = default);
}
