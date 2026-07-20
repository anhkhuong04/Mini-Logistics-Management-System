using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shops.Reports;

public sealed class ShopReportingService : IGetShopCodReportService, IGetShopDashboardKpiService
{
    private const int PageSize = 100;
    private const int MaxRows = 10_000;

    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentReadRepository _shipmentRepository;
    private readonly ICodTransactionRepository _codTransactionRepository;

    public ShopReportingService(
        IShopAccessService shopAccessService,
        IShipmentReadRepository shipmentRepository,
        ICodTransactionRepository codTransactionRepository)
    {
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
        _codTransactionRepository = codTransactionRepository;
    }

    public async Task<Result<ShopCodReportResponse>> GetAsync(
        GetShopCodReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopForUserAsync(
            query.OwnerUserId,
            query.ShopId,
            requireActiveShop: false,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShopCodReportResponse>.Failure(shopResult.Error);
        }

        var shipments = await LoadShipmentsAsync(shopResult.Value.Id, query.FromUtc, query.ToUtc, cancellationToken);
        var codByShipmentId = await _codTransactionRepository.GetByShipmentIdsAsync(
            shipments.Select(shipment => shipment.Id).ToList(),
            cancellationToken);
        var rows = shipments
            .Where(shipment => codByShipmentId.ContainsKey(shipment.Id))
            .Select(shipment =>
            {
                var cod = codByShipmentId[shipment.Id];
                return new ShopCodReportRowResponse(
                    shipment.Id,
                    shipment.TrackingCode.Value,
                    shipment.Status,
                    cod.Status,
                    cod.Amount.Amount,
                    cod.CollectedAmount?.Amount,
                    cod.DiscrepancyAmount?.Amount ?? 0m,
                    cod.CollectedAtUtc,
                    shipment.CreatedAtUtc);
            })
            .ToList();

        return Result<ShopCodReportResponse>.Success(new ShopCodReportResponse(
            shopResult.Value.Id,
            rows.Where(row => row.CodStatus == CodStatus.PendingCollection).Sum(row => row.DeclaredAmount),
            rows.Where(row => row.CodStatus is CodStatus.Collected or CodStatus.Settled).Sum(row => row.CollectedAmount ?? row.DeclaredAmount),
            rows.Where(row => row.CodStatus == CodStatus.Settled).Sum(row => row.CollectedAmount ?? row.DeclaredAmount),
            rows.Sum(row => row.DiscrepancyAmount),
            "VND",
            rows));
    }

    public async Task<Result<ShopDashboardKpiResponse>> GetAsync(
        ShopDashboardKpiQuery query,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopForUserAsync(
            query.OwnerUserId,
            query.ShopId,
            requireActiveShop: false,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShopDashboardKpiResponse>.Failure(shopResult.Error);
        }

        var shipments = await LoadShipmentsAsync(shopResult.Value.Id, query.FromUtc, query.ToUtc, cancellationToken);
        var codByShipmentId = await _codTransactionRepository.GetByShipmentIdsAsync(
            shipments.Select(shipment => shipment.Id).ToList(),
            cancellationToken);
        var nonDraftCount = shipments.Count(shipment => shipment.Status != ShipmentStatus.Draft);
        var deliveredCount = shipments.Count(shipment => shipment.Status == ShipmentStatus.Delivered);
        var returnedCount = shipments.Count(shipment => shipment.Status == ShipmentStatus.Returned);
        var failedCount = shipments.Count(shipment => shipment.Status == ShipmentStatus.DeliveryFailed);
        var countByStatus = shipments
            .GroupBy(shipment => shipment.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        return Result<ShopDashboardKpiResponse>.Success(new ShopDashboardKpiResponse(
            shopResult.Value.Id,
            shipments.Count,
            deliveredCount,
            returnedCount,
            failedCount,
            Rate(deliveredCount, nonDraftCount),
            Rate(returnedCount, nonDraftCount),
            Rate(failedCount, nonDraftCount),
            shipments.Sum(shipment => shipment.ShippingFee.Amount),
            codByShipmentId.Values.Where(cod => cod.Status == CodStatus.PendingCollection).Sum(cod => cod.Amount.Amount),
            codByShipmentId.Values.Where(cod => cod.Status is CodStatus.Collected or CodStatus.Settled).Sum(cod => cod.CollectedAmount?.Amount ?? cod.Amount.Amount),
            codByShipmentId.Values.Where(cod => cod.Status == CodStatus.Settled).Sum(cod => cod.CollectedAmount?.Amount ?? cod.Amount.Amount),
            "VND",
            countByStatus));
    }

    private async Task<IReadOnlyList<Shipment>> LoadShipmentsAsync(
        Guid shopId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        var shipments = new List<Shipment>();
        var pageNumber = 1;
        while (shipments.Count < MaxRows)
        {
            var page = await _shipmentRepository.SearchByShopAsync(
                new ShopShipmentSearchCriteria(
                    shopId,
                    StatusFilter: null,
                    TrackingCodeSearch: null,
                    ReceiverNameSearch: null,
                    ReceiverPhoneSearch: null,
                    fromUtc,
                    toUtc,
                    MinCodAmount: null,
                    MaxCodAmount: null,
                    ShopShipmentSortBy.CreatedAt,
                    SortDirection.Descending,
                    pageNumber,
                    PageSize),
                cancellationToken);
            shipments.AddRange(page.Items);

            if (pageNumber >= page.TotalPages || page.Items.Count == 0)
            {
                break;
            }

            pageNumber++;
        }

        return shipments;
    }

    private static decimal Rate(int count, int total)
    {
        return total == 0 ? 0m : decimal.Round((decimal)count / total * 100m, 2);
    }
}
