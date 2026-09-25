using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
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
    private readonly IAdministrativeDivisionService _administrativeDivisionService;
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;

    public UpdateShipmentBeforePickupService(
        IValidator<UpdateShipmentBeforePickupCommand> validator,
        IShippingFeeService shippingFeeService,
        IRouteClassificationService routeClassificationService,
        IShipmentRepository shipmentRepository,
        IShopAccessService shopAccessService,
        ICodTransactionRepository codTransactionRepository,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
        : this(
            validator,
            shippingFeeService,
            routeClassificationService,
            shipmentRepository,
            shopAccessService,
            codTransactionRepository,
            PassThroughAdministrativeDivisionService.Instance,
            timeProvider,
            adminAuditService)
    {
    }

    public UpdateShipmentBeforePickupService(
        IValidator<UpdateShipmentBeforePickupCommand> validator,
        IShippingFeeService shippingFeeService,
        IRouteClassificationService routeClassificationService,
        IShipmentRepository shipmentRepository,
        IShopAccessService shopAccessService,
        ICodTransactionRepository codTransactionRepository,
        IAdministrativeDivisionService administrativeDivisionService,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _shippingFeeService = shippingFeeService;
        _routeClassificationService = routeClassificationService;
        _shipmentRepository = shipmentRepository;
        _shopAccessService = shopAccessService;
        _codTransactionRepository = codTransactionRepository;
        _administrativeDivisionService = administrativeDivisionService;
        _timeProvider = timeProvider;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
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

        var shopResult = await _shopAccessService.GetShopAccessAsync(
            command.UserId,
            command.ShopId,
            requireActiveShop: true,
            ShopPermission.ManageShipments,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(shopResult.Error);
        }

        var shipment = await _shipmentRepository.GetTrackedByIdAndShopIdAsync(
            command.ShipmentId,
            shopResult.Value.Shop.Id,
            cancellationToken);
        if (shipment is null)
        {
            return Result<DraftShipmentResponse>.Failure(
                ApplicationErrors.NotFound("Shipment was not found for current shop."));
        }

        var normalizedCommandResult = await DraftShipmentMapping.NormalizeAddressesAsync(
            _administrativeDivisionService,
            command,
            cancellationToken);
        if (normalizedCommandResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(normalizedCommandResult.Error);
        }

        var normalizedCommand = normalizedCommandResult.Value;
        var previousFeeAmount = shipment.ShippingFee.Amount;
        var previousValue = new
        {
            Status = shipment.Status.ToString(),
            CodAmount = shipment.CodAmount.Amount,
            ShippingFee = shipment.ShippingFee.Amount
        };
        var calculatedResult = await DraftShipmentMapping.CalculateAsync(
            _routeClassificationService,
            _shippingFeeService,
            normalizedCommand,
            cancellationToken);
        if (calculatedResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(calculatedResult.Error);
        }

        var calculated = calculatedResult.Value;
        var now = _timeProvider.GetUtcNow();
        var updateResult = shipment.UpdateBeforePickup(
            normalizedCommand.SenderName,
            new PhoneNumber(normalizedCommand.SenderPhone),
            normalizedCommand.ReceiverName,
            new PhoneNumber(normalizedCommand.ReceiverPhone),
            DraftShipmentMapping.ToAddress(normalizedCommand.PickupAddress),
            DraftShipmentMapping.ToAddress(normalizedCommand.DeliveryAddress),
            calculated.Weight,
            calculated.ParcelDimensions,
            calculated.ChargeableWeight,
            calculated.GoodsValue,
            calculated.CodAmount,
            calculated.ShippingFeeBreakdown,
            calculated.RouteType,
            normalizedCommand.UserId,
            now,
            normalizedCommand.Note);
        if (updateResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(updateResult.Error);
        }

        shipment.RecordAppliedConfiguration(
            calculated.RouteClassification.PickupRouteRegionConfigId,
            calculated.RouteClassification.PickupRouteRegionConfigVersion,
            calculated.RouteClassification.DeliveryRouteRegionConfigId,
            calculated.RouteClassification.DeliveryRouteRegionConfigVersion,
            calculated.FeeQuote.FeeRuleId,
            calculated.FeeQuote.FeeRuleVersion);

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

        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.UserId,
                AdminAuditActions.ShipmentUpdatedBeforePickup,
                AdminAuditTargetTypes.Shipment,
                shipment.Id,
                OldValue: previousValue,
                NewValue: new
                {
                    Status = shipment.Status.ToString(),
                    CodAmount = shipment.CodAmount.Amount,
                    ShippingFee = shipment.ShippingFee.Amount
                },
                Reason: command.Note),
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result<DraftShipmentResponse>.Success(
            DraftShipmentMapping.ToResponse(shipment, previousFeeAmount));
    }
}
