using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;

public sealed class GetShipmentDetailForCurrentShopService : IGetShipmentDetailForCurrentShopService
{
    private readonly IIdentityService _identityService;
    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentReadRepository _shipmentRepository;

    public GetShipmentDetailForCurrentShopService(
        IIdentityService identityService,
        IShopAccessService shopAccessService,
        IShipmentReadRepository shipmentRepository)
    {
        _identityService = identityService;
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
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

        var shopResult = await _shopAccessService.GetShopForUserAsync(
            ownerUserId,
            shopId,
            requireActiveShop: false,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShipmentDetailResponse>.Failure(shopResult.Error);
        }

        var shipment = await _shipmentRepository.GetByIdAndShopIdAsync(
            shipmentId,
            shopResult.Value.Id,
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

        return Result<ShipmentDetailResponse>.Success(ToResponse(shipment, history));
    }

    private static ShipmentDetailResponse ToResponse(
        Shipment shipment,
        IReadOnlyList<ShipmentStatusHistoryResponse> trackingHistory)
    {
        return new ShipmentDetailResponse(
            shipment.Id,
            shipment.TrackingCode.Value,
            shipment.SenderName,
            shipment.SenderPhone.Value,
            shipment.ReceiverName,
            shipment.ReceiverPhone.Value,
            ToAddressResponse(shipment.PickupAddress),
            ToAddressResponse(shipment.DeliveryAddress),
            shipment.Weight.Kilograms,
            shipment.ParcelDimensions.LengthCm,
            shipment.ParcelDimensions.WidthCm,
            shipment.ParcelDimensions.HeightCm,
            shipment.ParcelDimensions.CalculateVolumetricWeightKg(),
            shipment.ChargeableWeight.Kilograms,
            shipment.GoodsValue.Amount,
            shipment.CodAmount.Amount,
            shipment.ShippingFeeBreakdown.BaseFee.Amount,
            shipment.ShippingFeeBreakdown.ExtraWeightFee.Amount,
            shipment.ShippingFeeBreakdown.InsuranceFee.Amount,
            shipment.ShippingFeeBreakdown.ReturnFee.Amount,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            shipment.RouteType,
            shipment.Note,
            shipment.Status,
            shipment.CreatedAtUtc,
            trackingHistory);
    }

    private static ShipmentAddressResponse ToAddressResponse(Address address)
    {
        return new ShipmentAddressResponse(
            address.Street,
            address.Ward,
            address.Province,
            address.Country,
            address.FullAddress);
    }

}
