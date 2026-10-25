using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.QuoteShipment;

public interface IQuoteShopShipmentService
{
    Task<Result<ShopShippingQuoteResponse>> QuoteAsync(QuoteShopShipmentCommand command, CancellationToken cancellationToken = default);
}

public sealed class QuoteShopShipmentService(IValidator<QuoteShopShipmentCommand> validator, IShopAccessService access,
    IAdministrativeDivisionService divisions, IRouteClassificationService routes, IShippingFeeService fees) : IQuoteShopShipmentService
{
    public async Task<Result<ShopShippingQuoteResponse>> QuoteAsync(QuoteShopShipmentCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid) return Result<ShopShippingQuoteResponse>.Failure(ApplicationErrors.ValidationFailed(
            string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        var shop = await access.GetShopAccessAsync(command.UserId, command.ShopId, true, ShopPermission.ManageShipments, cancellationToken);
        if (shop.IsFailure) return Result<ShopShippingQuoteResponse>.Failure(shop.Error);
        var pickup = await divisions.NormalizeProvinceWardAsync(command.PickupAddress.Province, command.PickupAddress.Ward, cancellationToken);
        if (pickup.IsFailure) return Result<ShopShippingQuoteResponse>.Failure(pickup.Error);
        var delivery = await divisions.NormalizeProvinceWardAsync(command.DeliveryAddress.Province, command.DeliveryAddress.Ward, cancellationToken);
        if (delivery.IsFailure) return Result<ShopShippingQuoteResponse>.Failure(delivery.Error);
        var route = routes.Classify(pickup.Value.Province, delivery.Value.Province);
        if (route.IsFailure) return Result<ShopShippingQuoteResponse>.Failure(route.Error);
        var quote = await fees.CalculateAsync(route.Value.RouteType, new Weight(command.WeightKg),
            new ParcelDimensions(command.LengthCm, command.WidthCm, command.HeightCm), new Money(command.GoodsValueAmount, command.Currency), cancellationToken);
        if (quote.IsFailure) return Result<ShopShippingQuoteResponse>.Failure(quote.Error);
        var value = quote.Value;
        return Result<ShopShippingQuoteResponse>.Success(new(route.Value.RouteType, value.ActualWeightKg,
            value.VolumetricWeightKg, value.ChargeableWeightKg, value.BaseFee.Amount, value.ExtraWeightFee.Amount,
            value.InsuranceFee.Amount, value.ReturnFee.Amount, value.TotalFee.Amount, value.TotalFee.Currency));
    }
}
