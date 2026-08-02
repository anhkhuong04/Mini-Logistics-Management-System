using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.Audit;

public sealed class GetShopAuditLogsService : IGetShopAuditLogsService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IShopAuditLogRepository _auditLogRepository;
    private readonly IPiiMaskingService _piiMaskingService;

    public GetShopAuditLogsService(
        IShopAccessService shopAccessService,
        IShopAuditLogRepository auditLogRepository,
        IPiiMaskingService piiMaskingService)
    {
        _shopAccessService = shopAccessService;
        _auditLogRepository = auditLogRepository;
        _piiMaskingService = piiMaskingService;
    }

    public async Task<Result<IReadOnlyList<ShopAuditLogResponse>>> GetAsync(
        ShopAuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var shopsResult = await _shopAccessService.GetAccessibleShopsAsync(
            query.CurrentUserId,
            cancellationToken);
        if (shopsResult.IsFailure)
        {
            return Result<IReadOnlyList<ShopAuditLogResponse>>.Failure(shopsResult.Error);
        }

        var shopIds = query.ShopId.HasValue
            ? shopsResult.Value.Where(shop => shop.Id == query.ShopId.Value).Select(shop => shop.Id).ToArray()
            : shopsResult.Value.Select(shop => shop.Id).ToArray();
        if (query.ShopId.HasValue && shopIds.Length == 0)
        {
            return Result<IReadOnlyList<ShopAuditLogResponse>>.Failure(
                ApplicationErrors.Forbidden("Current user cannot access this shop audit log."));
        }

        var logs = await _auditLogRepository.QueryForShopsAsync(
            query.CurrentUserId,
            shopIds,
            query with { Limit = Math.Clamp(query.Limit, 1, 500) },
            cancellationToken);
        return Result<IReadOnlyList<ShopAuditLogResponse>>.Success(logs
            .Select(log => new ShopAuditLogResponse(
                log.Id,
                log.ActorRole,
                log.Action,
                log.TargetType,
                log.TargetId,
                _piiMaskingService.MaskSensitiveJson(log.OldValueJson),
                _piiMaskingService.MaskSensitiveJson(log.NewValueJson),
                _piiMaskingService.MaskSensitiveText(log.Reason),
                log.CreatedAtUtc))
            .ToList());
    }
}
