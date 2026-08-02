using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.ImportShipments;

public sealed record ShipmentImportPreviewResponse(
    Guid ShopId,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    IReadOnlyList<ShipmentImportPreviewRowResponse> Rows);

public sealed record ShipmentImportPreviewRowResponse(
    ShipmentImportRowDraft Draft,
    bool IsValid,
    IReadOnlyList<string> Errors,
    RouteType? RouteType,
    decimal? ChargeableWeightKg,
    decimal? ShippingFeeAmount,
    string Currency);

public sealed record ShipmentImportConfirmResponse(
    int TotalRows,
    int CreatedRows,
    int FailedRows,
    IReadOnlyList<ShipmentImportConfirmRowResponse> Rows,
    Guid? BatchId = null,
    ShipmentImportBatchStatus? Status = null);

public sealed record ShipmentImportConfirmRowResponse(
    int RowNumber,
    string? ClientOrderCode,
    bool IsCreated,
    Guid? ShipmentId,
    string? TrackingCode,
    IReadOnlyList<string> Errors,
    ShipmentImportRowStatus Status = ShipmentImportRowStatus.Pending);
