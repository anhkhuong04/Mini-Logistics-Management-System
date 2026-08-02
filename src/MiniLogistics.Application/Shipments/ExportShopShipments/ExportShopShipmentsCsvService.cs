using System.Globalization;
using System.Text;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shipments.ExportShopShipments;

public sealed class ExportShopShipmentsCsvService : IExportShopShipmentsCsvService
{
    private const int PageSize = 100;
    private const int MaxRows = 10_000;

    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentReadRepository _shipmentRepository;
    private readonly IAdminAuditService _auditService;
    private readonly IPiiMaskingService _piiMaskingService;

    public ExportShopShipmentsCsvService(
        IShopAccessService shopAccessService,
        IShipmentReadRepository shipmentRepository,
        IAdminAuditService auditService,
        IPiiMaskingService? piiMaskingService = null)
    {
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
        _auditService = auditService;
        _piiMaskingService = piiMaskingService ?? new PiiMaskingService();
    }

    public async Task<Result<ExportShopShipmentsCsvResponse>> ExportAsync(
        ExportShopShipmentsCsvCommand command,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await _shopAccessService.GetShopAccessAsync(
            command.OwnerUserId,
            command.ShopId,
            requireActiveShop: false,
            ShopPermission.ExportData,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<ExportShopShipmentsCsvResponse>.Failure(accessResult.Error);
        }

        var shop = accessResult.Value.Shop;
        var canViewFullPii = accessResult.Value.HasPermission(ShopPermission.ViewFullPii);
        var canViewCod = accessResult.Value.HasPermission(ShopPermission.ViewCod);
        if (!canViewFullPii
            && (!string.IsNullOrWhiteSpace(command.ReceiverNameSearch)
                || !string.IsNullOrWhiteSpace(command.ReceiverPhoneSearch)
                || command.SortBy == ShopShipmentSortBy.ReceiverName))
        {
            return Result<ExportShopShipmentsCsvResponse>.Failure(
                ApplicationErrors.Forbidden("ViewFullPii permission is required to filter or sort exports by receiver data."));
        }

        if (!canViewCod
            && (command.MinCodAmount.HasValue
                || command.MaxCodAmount.HasValue
                || command.SortBy == ShopShipmentSortBy.CodAmount))
        {
            return Result<ExportShopShipmentsCsvResponse>.Failure(
                ApplicationErrors.Forbidden("ViewCod permission is required to filter or sort exports by COD data."));
        }
        var builder = new StringBuilder();
        builder.AppendLine("TrackingCode,CreatedAtUtc,ReceiverName,ReceiverPhone,Status,CodDeclaredAmount,ShippingFeeAmount,RouteType,PickupProvince,DeliveryProvince");

        var pageNumber = 1;
        var exportedRows = 0;
        while (exportedRows < MaxRows)
        {
            var page = await _shipmentRepository.SearchByShopAsync(
                new ShopShipmentSearchCriteria(
                    shop.Id,
                    command.StatusFilter,
                    command.TrackingCodeSearch,
                    command.ReceiverNameSearch,
                    command.ReceiverPhoneSearch,
                    command.FromUtc,
                    command.ToUtc,
                    command.MinCodAmount,
                    command.MaxCodAmount,
                    command.SortBy,
                    command.SortDirection,
                    pageNumber,
                    PageSize),
                cancellationToken);

            foreach (var shipment in page.Items)
            {
                builder
                    .Append(ToCsvField(shipment.TrackingCode.Value)).Append(',')
                    .Append(ToCsvField(shipment.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                    .Append(ToCsvField(canViewFullPii
                        ? shipment.ReceiverName
                        : _piiMaskingService.MaskName(shipment.ReceiverName))).Append(',')
                    .Append(ToCsvField(canViewFullPii
                        ? shipment.ReceiverPhone.Value
                        : _piiMaskingService.MaskPhone(shipment.ReceiverPhone.Value))).Append(',')
                    .Append(ToCsvField(shipment.Status.ToString())).Append(',')
                    .Append(canViewCod
                        ? shipment.CodAmount.Amount.ToString(CultureInfo.InvariantCulture)
                        : string.Empty).Append(',')
                    .Append(shipment.ShippingFee.Amount.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(ToCsvField(shipment.RouteType.ToString())).Append(',')
                    .Append(ToCsvField(shipment.PickupAddress.Province)).Append(',')
                    .Append(ToCsvField(shipment.DeliveryAddress.Province))
                    .AppendLine();
                exportedRows++;
            }

            if (pageNumber >= page.TotalPages || page.Items.Count == 0)
            {
                break;
            }

            pageNumber++;
        }

        await _auditService.RecordAsync(
            new AdminAuditEntry(
                command.OwnerUserId,
                AdminAuditActions.ShipmentExportCreated,
                AdminAuditTargetTypes.Shop,
                shop.Id,
                NewValue: new
                {
                    command.StatusFilter,
                    command.TrackingCodeSearch,
                    HasReceiverNameSearch = !string.IsNullOrWhiteSpace(command.ReceiverNameSearch),
                    HasReceiverPhoneSearch = !string.IsNullOrWhiteSpace(command.ReceiverPhoneSearch),
                    command.FromUtc,
                    command.ToUtc,
                    command.MinCodAmount,
                    command.MaxCodAmount,
                    command.SortBy,
                    command.SortDirection,
                    ExportedRows = exportedRows,
                    MaxRows
                },
                ActorRole: "Shop"),
            cancellationToken);
        await _auditService.SaveChangesAsync(cancellationToken);

        var fileName = $"shipments-{shop.Id:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return Result<ExportShopShipmentsCsvResponse>.Success(new ExportShopShipmentsCsvResponse(
            fileName,
            "text/csv; charset=utf-8",
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray()));
    }

    private static string ToCsvField(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
