using System.Globalization;
using System.Text;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.ExportShopShipments;

public sealed class ExportShopShipmentsCsvService : IExportShopShipmentsCsvService
{
    private const int PageSize = 100;
    private const int MaxRows = 10_000;

    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentReadRepository _shipmentRepository;

    public ExportShopShipmentsCsvService(
        IShopAccessService shopAccessService,
        IShipmentReadRepository shipmentRepository)
    {
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Result<ExportShopShipmentsCsvResponse>> ExportAsync(
        ExportShopShipmentsCsvCommand command,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopForUserAsync(
            command.OwnerUserId,
            command.ShopId,
            requireActiveShop: false,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ExportShopShipmentsCsvResponse>.Failure(shopResult.Error);
        }

        var shop = shopResult.Value;
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
                    ShopShipmentSortBy.CreatedAt,
                    SortDirection.Descending,
                    pageNumber,
                    PageSize),
                cancellationToken);

            foreach (var shipment in page.Items)
            {
                builder
                    .Append(ToCsvField(shipment.TrackingCode.Value)).Append(',')
                    .Append(ToCsvField(shipment.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                    .Append(ToCsvField(shipment.ReceiverName)).Append(',')
                    .Append(ToCsvField(shipment.ReceiverPhone.Value)).Append(',')
                    .Append(ToCsvField(shipment.Status.ToString())).Append(',')
                    .Append(shipment.CodAmount.Amount.ToString(CultureInfo.InvariantCulture)).Append(',')
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
