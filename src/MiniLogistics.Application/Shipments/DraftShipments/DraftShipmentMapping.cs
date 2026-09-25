using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.DraftShipments;

internal static class DraftShipmentMapping
{
    public static async Task<Result<TCommand>> NormalizeAddressesAsync<TCommand>(
        IAdministrativeDivisionService administrativeDivisionService,
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : IShipmentDetailsCommand
    {
        var pickup = await administrativeDivisionService.NormalizeProvinceWardAsync(
            command.PickupAddress.Province,
            command.PickupAddress.Ward,
            cancellationToken);
        if (pickup.IsFailure)
        {
            return Result<TCommand>.Failure(pickup.Error);
        }

        var delivery = await administrativeDivisionService.NormalizeProvinceWardAsync(
            command.DeliveryAddress.Province,
            command.DeliveryAddress.Ward,
            cancellationToken);
        if (delivery.IsFailure)
        {
            return Result<TCommand>.Failure(delivery.Error);
        }

        var pickupAddress = command.PickupAddress with
        {
            Province = pickup.Value.Province,
            Ward = pickup.Value.Ward
        };
        var deliveryAddress = command.DeliveryAddress with
        {
            Province = delivery.Value.Province,
            Ward = delivery.Value.Ward
        };

        return command switch
        {
            CreateDraftShipmentCommand create => Result<TCommand>.Success((TCommand)(IShipmentDetailsCommand)(create with
            {
                PickupAddress = pickupAddress,
                DeliveryAddress = deliveryAddress
            })),
            UpdateShipmentBeforePickupCommand update => Result<TCommand>.Success((TCommand)(IShipmentDetailsCommand)(update with
            {
                PickupAddress = pickupAddress,
                DeliveryAddress = deliveryAddress
            })),
            _ => throw new InvalidOperationException($"Unsupported shipment details command '{typeof(TCommand).Name}'.")
        };
    }

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
            routeClassificationResult.Value.RouteType,
            routeClassificationResult.Value,
            feeResult.Value));
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
    RouteType RouteType,
    RouteClassificationResult RouteClassification,
    ShippingFeeQuote FeeQuote);
