using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;

public sealed class GetShipmentDetailForCurrentShopService : IGetShipmentDetailForCurrentShopService
{
    private readonly IIdentityService _identityService;
    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentReadRepository _shipmentRepository;
    private readonly IPiiMaskingService _piiMaskingService;

    public GetShipmentDetailForCurrentShopService(
        IIdentityService identityService,
        IShopAccessService shopAccessService,
        IShipmentReadRepository shipmentRepository,
        IPiiMaskingService? piiMaskingService = null)
    {
        _identityService = identityService;
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
        _piiMaskingService = piiMaskingService ?? new PiiMaskingService();
    }

    public async Task<Result<ShipmentDetailResponse>> GetAsync(
        Guid ownerUserId,
        Guid shipmentId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default)
    {
        if (shipmentId == Guid.Empty)
        {
            return Result<ShipmentDetailResponse>.Failure(
                ApplicationErrors.ValidationFailed("Shipment id is required."));
        }

        var accessResult = await _shopAccessService.GetShopAccessAsync(
            ownerUserId,
            shopId,
            requireActiveShop: false,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<ShipmentDetailResponse>.Failure(accessResult.Error);
        }

        var shipment = await _shipmentRepository.GetByIdAndShopIdAsync(
            shipmentId,
            accessResult.Value.Shop.Id,
            cancellationToken);

        if (shipment is null)
        {
            return Result<ShipmentDetailResponse>.Failure(
                ApplicationErrors.NotFound("Shipment was not found for current shop."));
        }

        var history = await ShipmentStatusHistoryMapper.ToResponseAsync(
            shipment.StatusHistory,
            _identityService,
            cancellationToken);

        return Result<ShipmentDetailResponse>.Success(ToResponse(
            shipment,
            history,
            accessResult.Value.HasPermission(ShopPermission.ViewFullPii),
            accessResult.Value.HasPermission(ShopPermission.ViewCod)));
    }

    private ShipmentDetailResponse ToResponse(
        Shipment shipment,
        IReadOnlyList<ShipmentStatusHistoryResponse> trackingHistory,
        bool canViewFullPii,
        bool canViewCod)
    {
        return new ShipmentDetailResponse(
            shipment.Id,
            shipment.TrackingCode.Value,
            canViewFullPii ? shipment.SenderName : _piiMaskingService.MaskName(shipment.SenderName),
            canViewFullPii ? shipment.SenderPhone.Value : _piiMaskingService.MaskPhone(shipment.SenderPhone.Value),
            canViewFullPii ? shipment.ReceiverName : _piiMaskingService.MaskName(shipment.ReceiverName),
            canViewFullPii ? shipment.ReceiverPhone.Value : _piiMaskingService.MaskPhone(shipment.ReceiverPhone.Value),
            ToAddressResponse(shipment.PickupAddress, canViewFullPii),
            ToAddressResponse(shipment.DeliveryAddress, canViewFullPii),
            shipment.Weight.Kilograms,
            shipment.ParcelDimensions.LengthCm,
            shipment.ParcelDimensions.WidthCm,
            shipment.ParcelDimensions.HeightCm,
            shipment.ParcelDimensions.CalculateVolumetricWeightKg(),
            shipment.ChargeableWeight.Kilograms,
            shipment.GoodsValue.Amount,
            canViewCod ? shipment.CodAmount.Amount : 0m,
            shipment.ShippingFeeBreakdown.BaseFee.Amount,
            shipment.ShippingFeeBreakdown.ExtraWeightFee.Amount,
            shipment.ShippingFeeBreakdown.InsuranceFee.Amount,
            shipment.ShippingFeeBreakdown.ReturnFee.Amount,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            shipment.RouteType,
            canViewFullPii ? shipment.Note : _piiMaskingService.MaskSensitiveText(shipment.Note),
            shipment.Status,
            shipment.CreatedAtUtc,
            trackingHistory);
    }

    private ShipmentAddressResponse ToAddressResponse(Address address, bool canViewFullPii)
    {
        return new ShipmentAddressResponse(
            canViewFullPii ? address.Street : _piiMaskingService.MaskAddress(address.Street),
            address.Ward,
            address.Province,
            address.Country,
            canViewFullPii ? address.FullAddress : $"***, {address.Ward}, {address.Province}, {address.Country}");
    }

}
