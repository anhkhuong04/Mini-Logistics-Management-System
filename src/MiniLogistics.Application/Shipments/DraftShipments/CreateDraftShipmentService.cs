using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
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
    private readonly TimeProvider _timeProvider;

    public CreateDraftShipmentService(
        IValidator<CreateDraftShipmentCommand> validator,
        IShippingFeeService shippingFeeService,
        IRouteClassificationService routeClassificationService,
        IShipmentRepository shipmentRepository,
        IShopAccessService shopAccessService,
        TimeProvider timeProvider)
    {
        _validator = validator;
        _shippingFeeService = shippingFeeService;
        _routeClassificationService = routeClassificationService;
        _shipmentRepository = shipmentRepository;
        _shopAccessService = shopAccessService;
        _timeProvider = timeProvider;
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

        var shopResult = await _shopAccessService.GetShopForUserAsync(
            command.UserId,
            command.ShopId,
            requireActiveShop: true,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<DraftShipmentResponse>.Failure(shopResult.Error);
        }

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
        var trackingCode = await GenerateUniqueTrackingCodeAsync(now, cancellationToken);
        var shipment = Shipment.CreateDraft(
            shopResult.Value.Id,
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
            command.Note,
            trackingCode);

        await _shipmentRepository.AddAsync(shipment, cancellationToken);
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
