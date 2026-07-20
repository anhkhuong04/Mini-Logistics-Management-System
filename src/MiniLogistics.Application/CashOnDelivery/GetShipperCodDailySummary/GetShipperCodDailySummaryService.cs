using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.CashOnDelivery.GetShipperCodDailySummary;

public sealed class GetShipperCodDailySummaryService : IGetShipperCodDailySummaryService
{
    private readonly IIdentityService _identityService;
    private readonly IShipperCodDailySummaryRepository _summaryRepository;
    private readonly TimeProvider _timeProvider;

    public GetShipperCodDailySummaryService(
        IIdentityService identityService,
        IShipperCodDailySummaryRepository summaryRepository,
        TimeProvider timeProvider)
    {
        _identityService = identityService;
        _summaryRepository = summaryRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ShipperCodDailySummaryResponse>> GetAsync(
        Guid shipperUserId,
        DateOnly? businessDate = null,
        CancellationToken cancellationToken = default)
    {
        var shipperCheck = await _identityService.CheckUserRoleAsync(
            shipperUserId,
            nameof(UserRole.Shipper),
            cancellationToken);
        if (!shipperCheck.Exists)
        {
            return Result<ShipperCodDailySummaryResponse>.Failure(ApplicationErrors.NotFound("Shipper was not found."));
        }

        if (!shipperCheck.IsActive)
        {
            return Result<ShipperCodDailySummaryResponse>.Failure(ApplicationErrors.Forbidden("Shipper is not active."));
        }

        if (!shipperCheck.IsInRole)
        {
            return Result<ShipperCodDailySummaryResponse>.Failure(ApplicationErrors.Forbidden("Current user is not a shipper."));
        }

        var date = businessDate ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var startUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endUtc = startUtc.AddDays(1);

        return Result<ShipperCodDailySummaryResponse>.Success(
            await _summaryRepository.GetAsync(shipperUserId, startUtc, endUtc, cancellationToken));
    }
}
