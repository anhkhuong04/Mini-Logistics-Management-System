using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.DraftShipments;

public sealed class CreateDraftShipmentService : ICreateDraftShipmentService
{
    private const int MaxTrackingCodeGenerationAttempts = 5;

    private readonly IValidator<CreateDraftShipmentCommand> _validator;
    private readonly IShippingFeeService _shippingFeeService;
    private readonly IRouteClassificationService _routeClassificationService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IShopAccessService _shopAccessService;
    private readonly IAdministrativeDivisionService _administrativeDivisionService;
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;

    public CreateDraftShipmentService(
        IValidator<CreateDraftShipmentCommand> validator,
        IShippingFeeService shippingFeeService,
        IRouteClassificationService routeClassificationService,
        IShipmentRepository shipmentRepository,
        IShopAccessService shopAccessService,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
        : this(
            validator,
            shippingFeeService,
            routeClassificationService,
            shipmentRepository,
            shopAccessService,
            PassThroughAdministrativeDivisionService.Instance,
            timeProvider,
            adminAuditService)
    {
    }

    public CreateDraftShipmentService(
        IValidator<CreateDraftShipmentCommand> validator,
        IShippingFeeService shippingFeeService,
        IRouteClassificationService routeClassificationService,
        IShipmentRepository shipmentRepository,
        IShopAccessService shopAccessService,
        IAdministrativeDivisionService administrativeDivisionService,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _shippingFeeService = shippingFeeService;
        _routeClassificationService = routeClassificationService;
        _shipmentRepository = shipmentRepository;
        _shopAccessService = shopAccessService;
        _administrativeDivisionService = administrativeDivisionService;
        _timeProvider = timeProvider;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result<DraftShipmentResponse>> CreateAsync(
        CreateDraftShipmentCommand command,
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

        var normalizedCommandResult = await DraftShipmentMapping.NormalizeAddressesAsync(
            _administrativeDivisionService,
            command,
            cancellationToken);
        if (normalizedCommandResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(normalizedCommandResult.Error);
        }

        var normalizedCommand = normalizedCommandResult.Value;
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
        var trackingCode = await GenerateUniqueTrackingCodeAsync(now, cancellationToken);
        var shipment = Shipment.CreateDraft(
            shopResult.Value.Shop.Id,
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
            normalizedCommand.Note,
            trackingCode);
        shipment.RecordAppliedConfiguration(
            calculated.RouteClassification.PickupRouteRegionConfigId,
            calculated.RouteClassification.PickupRouteRegionConfigVersion,
            calculated.RouteClassification.DeliveryRouteRegionConfigId,
            calculated.RouteClassification.DeliveryRouteRegionConfigVersion,
            calculated.FeeQuote.FeeRuleId,
            calculated.FeeQuote.FeeRuleVersion);

        await _shipmentRepository.AddAsync(shipment, cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.UserId,
                AdminAuditActions.ShipmentDraftCreated,
                AdminAuditTargetTypes.Shipment,
                shipment.Id,
                NewValue: new
                {
                    shipment.ShopId,
                    TrackingCode = shipment.TrackingCode.Value,
                    Status = shipment.Status.ToString(),
                    CodAmount = shipment.CodAmount.Amount
                }),
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result<DraftShipmentResponse>.Success(DraftShipmentMapping.ToResponse(shipment));
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
}
