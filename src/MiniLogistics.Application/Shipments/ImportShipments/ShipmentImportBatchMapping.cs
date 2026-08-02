using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.ImportShipments;

internal static class ShipmentImportBatchMapping
{
    public static ShipmentImportConfirmResponse ToResponse(ShipmentImportBatch batch)
    {
        return new ShipmentImportConfirmResponse(
            batch.TotalRows,
            batch.CreatedRows,
            batch.FailedRows,
            batch.Rows
                .OrderBy(row => row.RowNumber)
                .Select(row => new ShipmentImportConfirmRowResponse(
                    row.RowNumber,
                    row.ClientOrderCode,
                    row.Status == ShipmentImportRowStatus.Created,
                    row.ShipmentId,
                    row.TrackingCode,
                    SplitErrors(row.Errors),
                    row.Status))
                .ToList(),
            batch.Id,
            batch.Status);
    }

    private static IReadOnlyList<string> SplitErrors(string? errors)
    {
        return string.IsNullOrWhiteSpace(errors)
            ? []
            : errors.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
