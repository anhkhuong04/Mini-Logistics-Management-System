using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shipments.GenerateShipmentLabel;

public sealed class GenerateShipmentLabelService : IGenerateShipmentLabelService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentReadRepository _shipmentRepository;

    private readonly IShipmentLabelRenderer _renderer;

    public GenerateShipmentLabelService(
        IShopAccessService shopAccessService,
        IShipmentReadRepository shipmentRepository,
        IShipmentLabelRenderer renderer)
    {
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
        _renderer = renderer;
    }

    public async Task<Result<ShipmentLabelResponse>> GenerateAsync(
        GenerateShipmentLabelCommand command,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopAccessAsync(
            command.OwnerUserId,
            command.ShopId,
            requireActiveShop: false,
            ShopPermission.ViewShipments | ShopPermission.ViewFullPii,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShipmentLabelResponse>.Failure(shopResult.Error);
        }

        var shipment = await _shipmentRepository.GetByIdAndShopIdAsync(
            command.ShipmentId,
            shopResult.Value.Shop.Id,
            cancellationToken);
        if (shipment is null)
        {
            return Result<ShipmentLabelResponse>.Failure(ApplicationErrors.NotFound("Shipment was not found for current shop."));
        }

        if (shipment.Status == ShipmentStatus.Draft)
        {
            return Result<ShipmentLabelResponse>.Failure(ApplicationErrors.ValidationFailed("Draft shipment does not have a shipping label."));
        }

        var pdf = _renderer.Render(shipment);

        return Result<ShipmentLabelResponse>.Success(new ShipmentLabelResponse(
            $"label-{shipment.TrackingCode.Value}.pdf",
            "application/pdf",
            pdf));
    }
}
