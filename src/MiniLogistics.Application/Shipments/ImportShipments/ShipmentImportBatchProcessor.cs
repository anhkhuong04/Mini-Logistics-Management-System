using System.Text.Json;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shipments.ImportShipments;

public sealed class ShipmentImportBatchProcessor
{
    private readonly IShipmentImportBatchRepository _batchRepository;
    private readonly IShopAccessService _shopAccessService;
    private readonly ICreateShipmentService _createShipmentService;
    private readonly IApplicationDbTransactionManager _transactionManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ShipmentImportBatchProcessor> _logger;

    public ShipmentImportBatchProcessor(
        IShipmentImportBatchRepository batchRepository,
        IShopAccessService shopAccessService,
        ICreateShipmentService createShipmentService,
        IApplicationDbTransactionManager transactionManager,
        TimeProvider timeProvider,
        ILogger<ShipmentImportBatchProcessor> logger)
    {
        _batchRepository = batchRepository;
        _shopAccessService = shopAccessService;
        _createShipmentService = createShipmentService;
        _transactionManager = transactionManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<bool> ProcessNextRowAsync(CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetNextProcessableAsync(cancellationToken);
        if (batch is null)
        {
            return false;
        }

        var row = batch.Rows
            .Where(item => item.Status is ShipmentImportRowStatus.Pending or ShipmentImportRowStatus.Processing)
            .OrderBy(item => item.RowNumber)
            .First();
        var now = _timeProvider.GetUtcNow();
        batch.MarkProcessing(now);
        row.MarkProcessing(now);

        await using var transaction = await _transactionManager.BeginTransactionAsync(cancellationToken);
        var shopResult = await _shopAccessService.GetShopAccessAsync(
            batch.RequestedByUserId,
            batch.ShopId,
            requireActiveShop: true,
            ShopPermission.ManageShipments,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            row.MarkFailed(shopResult.Error.Description, now);
            batch.RefreshProgress(now);
            await _batchRepository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        ShipmentImportRowDraft? draft;
        try
        {
            draft = JsonSerializer.Deserialize<ShipmentImportRowDraft>(row.PayloadJson);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Import row {ImportRowId} contains invalid payload JSON.", row.Id);
            draft = null;
        }

        if (draft is null)
        {
            row.MarkFailed("Stored import payload is invalid.", now);
        }
        else if (!string.IsNullOrWhiteSpace(draft.ClientOrderCode)
                 && await _batchRepository.HasCreatedClientOrderAsync(
                     batch.ShopId,
                     draft.ClientOrderCode,
                     cancellationToken))
        {
            row.MarkFailed($"clientOrderCode '{draft.ClientOrderCode}' was already imported.", now);
        }
        else
        {
            var createResult = await _createShipmentService.CreateAsync(
                ShipmentImportService.BuildCreateShipmentCommand(
                    batch.RequestedByUserId,
                    shopResult.Value.Shop,
                    draft),
                cancellationToken);
            if (createResult.IsSuccess)
            {
                row.MarkCreated(
                    createResult.Value.ShipmentId,
                    createResult.Value.TrackingCode,
                    _timeProvider.GetUtcNow());
            }
            else
            {
                row.MarkFailed(createResult.Error.Description, _timeProvider.GetUtcNow());
            }
        }

        batch.RefreshProgress(_timeProvider.GetUtcNow());
        await _batchRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
