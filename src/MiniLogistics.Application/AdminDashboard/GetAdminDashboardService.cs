using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminDashboard;

public sealed class GetAdminDashboardService : IGetAdminDashboardService
{
    private readonly IIdentityService _identityService;
    private readonly IAdminDashboardMetricsRepository _metricsRepository;
    private readonly TimeProvider _timeProvider;

    public GetAdminDashboardService(
        IIdentityService identityService,
        IAdminDashboardMetricsRepository metricsRepository,
        TimeProvider timeProvider)
    {
        _identityService = identityService;
        _metricsRepository = metricsRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AdminDashboardResponse>> GetAsync(
        AdminDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.RequestedByUserId == Guid.Empty)
        {
            return Result<AdminDashboardResponse>.Failure(
                ApplicationErrors.ValidationFailed("Requested by user id is required."));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            query.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<AdminDashboardResponse>.Failure(authorizationResult.Error);
        }

        var now = _timeProvider.GetUtcNow();
        var fromUtc = query.FromUtc ?? new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var toUtc = query.ToUtc ?? now;
        if (toUtc < fromUtc)
        {
            return Result<AdminDashboardResponse>.Failure(
                ApplicationErrors.ValidationFailed("To date must be greater than or equal to from date."));
        }

        var activeShippers = await _identityService.GetActiveShippersAsync(cancellationToken);
        var shipperCapacities = activeShippers
            .Select(shipper => new ActiveShipperCapacity(
                shipper.UserId,
                shipper.IsAvailableForAssignment,
                shipper.MaxActiveShipments))
            .ToList();

        var metrics = await _metricsRepository.GetAsync(
            fromUtc,
            toUtc,
            NormalizeOptional(query.Province),
            shipperCapacities,
            cancellationToken);

        return Result<AdminDashboardResponse>.Success(new AdminDashboardResponse(
            fromUtc,
            toUtc,
            NormalizeOptional(query.Province),
            metrics.Shipments,
            metrics.Shippers,
            metrics.Cod,
            metrics.Shops,
            metrics.Webhooks));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
