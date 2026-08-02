using System.Text;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.ImportShipments;

public sealed class ShipmentImportBatchQueryService :
    IGetShipmentImportBatchService,
    IExportShipmentImportErrorsService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentImportBatchRepository _batchRepository;

    public ShipmentImportBatchQueryService(
        IShopAccessService shopAccessService,
        IShipmentImportBatchRepository batchRepository)
    {
        _shopAccessService = shopAccessService;
        _batchRepository = batchRepository;
    }

    public async Task<Result<ShipmentImportConfirmResponse>> GetAsync(
        Guid currentUserId,
        Guid? shopId,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batchResult = await GetOwnedBatchAsync(currentUserId, shopId, batchId, cancellationToken);
        return batchResult.IsFailure
            ? Result<ShipmentImportConfirmResponse>.Failure(batchResult.Error)
            : Result<ShipmentImportConfirmResponse>.Success(ShipmentImportBatchMapping.ToResponse(batchResult.Value));
    }

    public async Task<Result<string>> ExportCsvAsync(
        Guid currentUserId,
        Guid? shopId,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batchResult = await GetOwnedBatchAsync(currentUserId, shopId, batchId, cancellationToken);
        if (batchResult.IsFailure)
        {
            return Result<string>.Failure(batchResult.Error);
        }

        var builder = new StringBuilder();
        builder.AppendLine("rowNumber,clientOrderCode,status,errors");
        foreach (var row in batchResult.Value.Rows
                     .Where(row => row.Status == ShipmentImportRowStatus.Failed)
                     .OrderBy(row => row.RowNumber))
        {
            builder.Append(row.RowNumber).Append(',')
                .Append(Escape(row.ClientOrderCode)).Append(',')
                .Append(row.Status).Append(',')
                .Append(Escape(row.Errors)).AppendLine();
        }

        return Result<string>.Success(builder.ToString());
    }

    private async Task<Result<ShipmentImportBatch>> GetOwnedBatchAsync(
        Guid currentUserId,
        Guid? shopId,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        if (batchId == Guid.Empty)
        {
            return Result<ShipmentImportBatch>.Failure(
                ApplicationErrors.ValidationFailed("Import batch id is required."));
        }

        var shopResult = await _shopAccessService.GetShopForUserAsync(
            currentUserId,
            shopId,
            requireActiveShop: false,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShipmentImportBatch>.Failure(shopResult.Error);
        }

        var batch = await _batchRepository.GetByIdForShopAsync(
            batchId,
            shopResult.Value.Id,
            cancellationToken);
        return batch is null
            ? Result<ShipmentImportBatch>.Failure(ApplicationErrors.NotFound("Import batch was not found for current shop."))
            : Result<ShipmentImportBatch>.Success(batch);
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        return text.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? text
            : $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
