using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;

public sealed class GetShipmentDetailForCurrentShopService : IGetShipmentDetailForCurrentShopService
{
    private readonly IShopRepository _shopRepository;
    private readonly IShipmentRepository _shipmentRepository;

    public GetShipmentDetailForCurrentShopService(
        IShopRepository shopRepository,
        IShipmentRepository shipmentRepository)
    {
        _shopRepository = shopRepository;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Result<ShipmentDetailResponse>> GetAsync(
        Guid ownerUserId,
        Guid shipmentId,
        CancellationToken cancellationToken = default)
    {
        if (ownerUserId == Guid.Empty)
        {
            return Result<ShipmentDetailResponse>.Failure(
                ApplicationErrors.ValidationFailed("Current user id is required."));
        }

        if (shipmentId == Guid.Empty)
        {
            return Result<ShipmentDetailResponse>.Failure(
                ApplicationErrors.ValidationFailed("Shipment id is required."));
        }

        var shop = await _shopRepository.GetByOwnerUserIdAsync(ownerUserId, cancellationToken);
        if (shop is null)
        {
            return Result<ShipmentDetailResponse>.Failure(
                ApplicationErrors.NotFound("Shop was not found for current user."));
        }

        if (!shop.IsActive)
        {
            return Result<ShipmentDetailResponse>.Failure(
                ApplicationErrors.Forbidden("Shop account is not active."));
        }

        var shipment = await _shipmentRepository.GetByIdAndShopIdAsync(
            shipmentId,
            shop.Id,
            cancellationToken);

        if (shipment is null)
        {
            return Result<ShipmentDetailResponse>.Failure(
                ApplicationErrors.NotFound("Shipment was not found for current shop."));
        }

        return Result<ShipmentDetailResponse>.Success(ToResponse(shipment));
    }

    private static ShipmentDetailResponse ToResponse(Shipment shipment)
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
            ToHistoryResponse(shipment.StatusHistory));
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

    private static IReadOnlyList<ShipmentStatusHistoryResponse> ToHistoryResponse(
        IEnumerable<ShipmentStatusHistory> statusHistory)
    {
        return statusHistory
            .OrderBy(history => history.ChangedAtUtc)
            .Select(history => new ShipmentStatusHistoryResponse(
                history.Status,
                history.Note,
                history.ChangedAtUtc))
            .ToList();
    }
}
