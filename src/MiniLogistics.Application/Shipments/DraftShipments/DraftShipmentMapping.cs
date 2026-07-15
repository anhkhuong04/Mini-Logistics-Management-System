using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.DraftShipments;

internal static class DraftShipmentMapping
{
    public static async Task<Result<DraftShipmentCalculatedValues>> CalculateAsync(
        IRouteClassificationService routeClassificationService,
        IShippingFeeService shippingFeeService,
        IShipmentDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var weight = new Weight(command.WeightKg);
        var parcelDimensions = new ParcelDimensions(
            command.LengthCm,
            command.WidthCm,
            command.HeightCm);
        var goodsValue = new Money(command.GoodsValueAmount, command.Currency);
        var codAmount = new Money(command.CodAmount, command.Currency);
        var routeClassificationResult = routeClassificationService.Classify(
            command.PickupAddress.Province,
            command.DeliveryAddress.Province);

        if (routeClassificationResult.IsFailure)
        {
            return Result<DraftShipmentCalculatedValues>.Failure(routeClassificationResult.Error);
        }

        var feeResult = await shippingFeeService.CalculateAsync(
            routeClassificationResult.Value.RouteType,
            weight,
            parcelDimensions,
            goodsValue,
            cancellationToken);

        if (feeResult.IsFailure)
        {
            return Result<DraftShipmentCalculatedValues>.Failure(feeResult.Error);
        }

        return Result<DraftShipmentCalculatedValues>.Success(new DraftShipmentCalculatedValues(
            weight,
            parcelDimensions,
            new Weight(feeResult.Value.ChargeableWeightKg),
            goodsValue,
            codAmount,
            feeResult.Value.Breakdown,
            routeClassificationResult.Value.RouteType));
    }

    public static DraftShipmentResponse ToResponse(
        Shipment shipment,
        decimal? previousShippingFeeAmount = null)
    {
        return new DraftShipmentResponse(
            shipment.Id,
            shipment.TrackingCode.Value,
            shipment.Weight.Kilograms,
            shipment.ParcelDimensions.CalculateVolumetricWeightKg(),
            shipment.ChargeableWeight.Kilograms,
            shipment.ShippingFeeBreakdown.BaseFee.Amount,
            shipment.ShippingFeeBreakdown.ExtraWeightFee.Amount,
            shipment.ShippingFeeBreakdown.InsuranceFee.Amount,
            shipment.ShippingFeeBreakdown.ReturnFee.Amount,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            shipment.Status,
            previousShippingFeeAmount);
    }

    public static Address ToAddress(ShipmentAddressDto address)
    {
        return new Address(
            address.Street,
            address.Ward,
            address.Province,
            address.Country);
    }
}

internal sealed record DraftShipmentCalculatedValues(
    Weight Weight,
    ParcelDimensions ParcelDimensions,
    Weight ChargeableWeight,
    Money GoodsValue,
    Money CodAmount,
    ShippingFeeBreakdown ShippingFeeBreakdown,
    RouteType RouteType);
