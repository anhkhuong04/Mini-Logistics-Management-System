using FluentValidation;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.CreateShipment;

public sealed class CreateShipmentService : ICreateShipmentService
{
    private const int MaxTrackingCodeGenerationAttempts = 5;

    private readonly IValidator<CreateShipmentCommand> _validator;
    private readonly IShippingFeeService _shippingFeeService;
    private readonly IRouteClassificationService _routeClassificationService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IShopAccessService _shopAccessService;
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly IAutoAssignShipmentService _autoAssignShipmentService;
    private readonly TimeProvider _timeProvider;

    public CreateShipmentService(
        IValidator<CreateShipmentCommand> validator,
        IShippingFeeService shippingFeeService,
        IRouteClassificationService routeClassificationService,
        IShipmentRepository shipmentRepository,
        IShopAccessService shopAccessService,
        ICodTransactionRepository codTransactionRepository,
        IAutoAssignShipmentService autoAssignShipmentService,
        TimeProvider timeProvider)
    {
        _validator = validator;
        _shippingFeeService = shippingFeeService;
        _routeClassificationService = routeClassificationService;
        _shipmentRepository = shipmentRepository;
        _shopAccessService = shopAccessService;
        _codTransactionRepository = codTransactionRepository;
        _autoAssignShipmentService = autoAssignShipmentService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CreateShipmentResponse>> CreateAsync(
        CreateShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<CreateShipmentResponse>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var shopResult = await _shopAccessService.GetShopForUserAsync(
            command.CreatedByUserId,
            command.ShopId,
            requireActiveShop: true,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<CreateShipmentResponse>.Failure(shopResult.Error);
        }

        var shop = shopResult.Value;
        var weight = new Weight(command.WeightKg);
        var parcelDimensions = new ParcelDimensions(
            command.LengthCm,
            command.WidthCm,
            command.HeightCm);
        var goodsValue = new Money(command.GoodsValueAmount, command.Currency);
        var codAmount = new Money(command.CodAmount, command.Currency);
        var routeClassificationResult = _routeClassificationService.Classify(
            command.PickupAddress.Province,
            command.DeliveryAddress.Province);

        if (routeClassificationResult.IsFailure)
        {
            return Result<CreateShipmentResponse>.Failure(routeClassificationResult.Error);
        }

        var routeType = routeClassificationResult.Value.RouteType;

        var shippingFeeResult = await _shippingFeeService.CalculateAsync(
            routeType,
            weight,
            parcelDimensions,
            goodsValue,
            cancellationToken);

        if (shippingFeeResult.IsFailure)
        {
            return Result<CreateShipmentResponse>.Failure(shippingFeeResult.Error);
        }

        var now = _timeProvider.GetUtcNow();
        var trackingCode = await GenerateUniqueTrackingCodeAsync(now, cancellationToken);
        var shipment = Shipment.Create(
            shop.Id,
            command.SenderName,
            new PhoneNumber(command.SenderPhone),
            command.ReceiverName,
            new PhoneNumber(command.ReceiverPhone),
            ToAddress(command.PickupAddress),
            ToAddress(command.DeliveryAddress),
            weight,
            parcelDimensions,
            new Weight(shippingFeeResult.Value.ChargeableWeightKg),
            goodsValue,
            codAmount,
            shippingFeeResult.Value.Breakdown,
            routeType,
            command.CreatedByUserId,
            now,
            command.Note,
            trackingCode);
        var codTransaction = CodTransaction.Create(shipment.Id, codAmount, now);

        await _shipmentRepository.AddAsync(shipment, cancellationToken);
        await _codTransactionRepository.AddAsync(codTransaction, cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);
        await _autoAssignShipmentService.AutoAssignAsync(shipment.Id, cancellationToken);

        return Result<CreateShipmentResponse>.Success(new CreateShipmentResponse(
            shipment.Id,
            shipment.TrackingCode.Value,
            shipment.Weight.Kilograms,
            parcelDimensions.CalculateVolumetricWeightKg(),
            shipment.ChargeableWeight.Kilograms,
            shipment.ShippingFeeBreakdown.BaseFee.Amount,
            shipment.ShippingFeeBreakdown.ExtraWeightFee.Amount,
            shipment.ShippingFeeBreakdown.InsuranceFee.Amount,
            shipment.ShippingFeeBreakdown.ReturnFee.Amount,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            shipment.Status));
    }

    private async Task<TrackingCode> GenerateUniqueTrackingCodeAsync(
        DateTimeOffset generatedAtUtc,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxTrackingCodeGenerationAttempts; attempt++)
        {
            var trackingCode = TrackingCode.Generate(generatedAtUtc);
            var exists = await _shipmentRepository.ExistsByTrackingCodeAsync(trackingCode, cancellationToken);

            if (!exists)
            {
                return trackingCode;
            }
        }

        throw new InvalidOperationException("Could not generate a unique tracking code.");
    }

    private static Address ToAddress(ShipmentAddressDto address)
    {
        return new Address(
            address.Street,
            address.Ward,
            address.Province,
            address.Country);
    }
}
