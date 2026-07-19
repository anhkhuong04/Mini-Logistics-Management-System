using FluentValidation;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.DraftShipments;

public sealed class UpdateShipmentBeforePickupService : IUpdateShipmentBeforePickupService
{
    private readonly IValidator<UpdateShipmentBeforePickupCommand> _validator;
    private readonly IShippingFeeService _shippingFeeService;
    private readonly IRouteClassificationService _routeClassificationService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IShopAccessService _shopAccessService;
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly TimeProvider _timeProvider;

    public UpdateShipmentBeforePickupService(
        IValidator<UpdateShipmentBeforePickupCommand> validator,
        IShippingFeeService shippingFeeService,
        IRouteClassificationService routeClassificationService,
        IShipmentRepository shipmentRepository,
        IShopAccessService shopAccessService,
        ICodTransactionRepository codTransactionRepository,
        TimeProvider timeProvider)
    {
        _validator = validator;
        _shippingFeeService = shippingFeeService;
        _routeClassificationService = routeClassificationService;
        _shipmentRepository = shipmentRepository;
        _shopAccessService = shopAccessService;
        _codTransactionRepository = codTransactionRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<DraftShipmentResponse>> UpdateAsync(
        UpdateShipmentBeforePickupCommand command,
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
        var calculatedResult = await DraftShipmentMapping.CalculateAsync(
            _routeClassificationService,
            _shippingFeeService,
            command,
            cancellationToken);
        if (calculatedResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(calculatedResult.Error);
        }

        var calculated = calculatedResult.Value;
        var now = _timeProvider.GetUtcNow();
        var updateResult = shipment.UpdateBeforePickup(
            command.SenderName,
            new PhoneNumber(command.SenderPhone),
            command.ReceiverName,
            new PhoneNumber(command.ReceiverPhone),
            DraftShipmentMapping.ToAddress(command.PickupAddress),
            DraftShipmentMapping.ToAddress(command.DeliveryAddress),
            calculated.Weight,
            calculated.ParcelDimensions,
            calculated.ChargeableWeight,
            calculated.GoodsValue,
            calculated.CodAmount,
            calculated.ShippingFeeBreakdown,
            calculated.RouteType,
            command.UserId,
            now,
            command.Note);
        if (updateResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(updateResult.Error);
        }

        if (shipment.Status == ShipmentStatus.PendingPickup)
        {
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
        }

        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result<DraftShipmentResponse>.Success(
            DraftShipmentMapping.ToResponse(shipment, previousFeeAmount));
    }
}
