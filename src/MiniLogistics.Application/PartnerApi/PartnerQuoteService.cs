using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerQuoteService : IPartnerQuoteService
{
    private readonly IValidator<PartnerQuoteCommand> _validator;
    private readonly IShopRepository _shopRepository;
    private readonly IRouteClassificationService _routeClassificationService;
    private readonly IShippingFeeService _shippingFeeService;

    public PartnerQuoteService(
        IValidator<PartnerQuoteCommand> validator,
        IShopRepository shopRepository,
        IRouteClassificationService routeClassificationService,
        IShippingFeeService shippingFeeService)
    {
        _validator = validator;
        _shopRepository = shopRepository;
        _routeClassificationService = routeClassificationService;
        _shippingFeeService = shippingFeeService;
    }

    public async Task<Result<PartnerShippingQuoteResponse>> QuoteAsync(
        PartnerQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<PartnerShippingQuoteResponse>.Failure(ToValidationError(validationResult.Errors));
        }

        var shop = await _shopRepository.GetByIdAsync(command.ShopId, cancellationToken);
        if (shop is null)
        {
            return Result<PartnerShippingQuoteResponse>.Failure(ApplicationErrors.NotFound("Shop was not found for API client."));
        }

        if (!shop.IsActive)
        {
            return Result<PartnerShippingQuoteResponse>.Failure(ApplicationErrors.Forbidden("Shop account is not active."));
        }

        var pickupAddress = command.PickupAddress ?? ToDto(shop.Address);
        var routeResult = _routeClassificationService.Classify(
            pickupAddress.Province,
            command.DeliveryAddress.Province);
        if (routeResult.IsFailure)
        {
            return Result<PartnerShippingQuoteResponse>.Failure(routeResult.Error);
        }

        var feeResult = await _shippingFeeService.CalculateAsync(
            routeResult.Value.RouteType,
            new Weight(command.WeightKg),
            new ParcelDimensions(command.LengthCm, command.WidthCm, command.HeightCm),
            new Money(command.GoodsValueAmount, command.Currency),
            cancellationToken);
        if (feeResult.IsFailure)
        {
            return Result<PartnerShippingQuoteResponse>.Failure(feeResult.Error);
        }

        return Result<PartnerShippingQuoteResponse>.Success(new PartnerShippingQuoteResponse(
            routeResult.Value.RouteType,
            feeResult.Value.ActualWeightKg,
            feeResult.Value.VolumetricWeightKg,
            feeResult.Value.ChargeableWeightKg,
            feeResult.Value.BaseFee.Amount,
            feeResult.Value.ExtraWeightFee.Amount,
            feeResult.Value.InsuranceFee.Amount,
            feeResult.Value.ReturnFee.Amount,
            feeResult.Value.TotalFee.Amount,
            feeResult.Value.TotalFee.Currency));
    }

    private static ShipmentAddressDto ToDto(Address address)
    {
        return new ShipmentAddressDto(
            address.Street,
            address.Ward,
            address.Province,
            address.Country);
    }

    private static Error ToValidationError(IEnumerable<FluentValidation.Results.ValidationFailure> errors)
    {
        return ApplicationErrors.ValidationFailed(string.Join("; ", errors.Select(error => error.ErrorMessage)));
    }
}
