using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.ExportShopShipments;

public interface IExportShopShipmentsCsvService
{
    Task<Result<ExportShopShipmentsCsvResponse>> ExportAsync(
        ExportShopShipmentsCsvCommand command,
        CancellationToken cancellationToken = default);
}
