using FluentValidation;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.DraftShipments;

public sealed class SubmitDraftShipmentService : ISubmitDraftShipmentService
{
    private readonly IValidator<SubmitDraftShipmentCommand> _validator;
    private readonly IShippingFeeService _shippingFeeService;
    private readonly IRouteClassificationService _routeClassificationService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IShopAccessService _shopAccessService;
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly IAutoAssignShipmentService _autoAssignShipmentService;
    private readonly TimeProvider _timeProvider;

    public SubmitDraftShipmentService(
        IValidator<SubmitDraftShipmentCommand> validator,
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

    public async Task<Result<DraftShipmentResponse>> SubmitAsync(
        SubmitDraftShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<DraftShipmentResponse>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var shopResult = await _shopAccessService.GetShopForUserAsync(
            command.UserId,
            command.ShopId,
            requireActiveShop: true,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(shopResult.Error);
        }

        var shipment = await _shipmentRepository.GetTrackedByIdAndShopIdAsync(
            command.ShipmentId,
            shopResult.Value.Id,
            cancellationToken);
        if (shipment is null)
        {
            return Result<DraftShipmentResponse>.Failure(
                ApplicationErrors.NotFound("Shipment was not found for current shop."));
        }

        var previousFeeAmount = shipment.ShippingFee.Amount;
        var repricingCommand = new SubmitDraftRepricingCommand(command.UserId, shipment, command.ShopId);
        var calculatedResult = await DraftShipmentMapping.CalculateAsync(
            _routeClassificationService,
            _shippingFeeService,
            repricingCommand,
            cancellationToken);
        if (calculatedResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(calculatedResult.Error);
        }

        var calculated = calculatedResult.Value;
        var now = _timeProvider.GetUtcNow();
        var updateResult = shipment.UpdateBeforePickup(
            shipment.SenderName,
            shipment.SenderPhone,
            shipment.ReceiverName,
            shipment.ReceiverPhone,
            shipment.PickupAddress,
            shipment.DeliveryAddress,
            calculated.Weight,
            calculated.ParcelDimensions,
            calculated.ChargeableWeight,
            calculated.GoodsValue,
            calculated.CodAmount,
            calculated.ShippingFeeBreakdown,
            calculated.RouteType,
            command.UserId,
            now,
            shipment.Note);
        if (updateResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(updateResult.Error);
        }

        var submitResult = shipment.SubmitDraft(command.UserId, now);
        if (submitResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(submitResult.Error);
        }

        var codTransaction = await _codTransactionRepository.GetTrackedByShipmentIdAsync(
            shipment.Id,
            cancellationToken);
        if (codTransaction is null)
        {
            await _codTransactionRepository.AddAsync(
                CodTransaction.Create(shipment.Id, calculated.CodAmount, now),
                cancellationToken);
        }
        else
        {
            var codResult = codTransaction.UpdateAmount(calculated.CodAmount, now);
            if (codResult.IsFailure)
            {
                return Result<DraftShipmentResponse>.Failure(codResult.Error);
            }
        }

        await _shipmentRepository.SaveChangesAsync(cancellationToken);
        await _autoAssignShipmentService.AutoAssignAsync(shipment.Id, cancellationToken);

        return Result<DraftShipmentResponse>.Success(
            DraftShipmentMapping.ToResponse(shipment, previousFeeAmount));
    }

    private sealed record SubmitDraftRepricingCommand(
        Guid UserId,
        Shipment Shipment,
        Guid? ShopId) : IShipmentDetailsCommand
    {
        public string SenderName => Shipment.SenderName;

        public string SenderPhone => Shipment.SenderPhone.Value;

        public string ReceiverName => Shipment.ReceiverName;

        public string ReceiverPhone => Shipment.ReceiverPhone.Value;

        public ShipmentAddressDto PickupAddress => new(
            Shipment.PickupAddress.Street,
            Shipment.PickupAddress.Ward,
            Shipment.PickupAddress.Province,
            Shipment.PickupAddress.Country);

        public ShipmentAddressDto DeliveryAddress => new(
            Shipment.DeliveryAddress.Street,
            Shipment.DeliveryAddress.Ward,
            Shipment.DeliveryAddress.Province,
            Shipment.DeliveryAddress.Country);

        public decimal WeightKg => Shipment.Weight.Kilograms;

        public decimal LengthCm => Shipment.ParcelDimensions.LengthCm;

        public decimal WidthCm => Shipment.ParcelDimensions.WidthCm;

        public decimal HeightCm => Shipment.ParcelDimensions.HeightCm;

        public decimal GoodsValueAmount => Shipment.GoodsValue.Amount;

        public decimal CodAmount => Shipment.CodAmount.Amount;

        public string Currency => Shipment.ShippingFee.Currency;

        public string? Note => Shipment.Note;
    }
}
