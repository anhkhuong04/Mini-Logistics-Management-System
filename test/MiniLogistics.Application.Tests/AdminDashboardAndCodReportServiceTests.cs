using MiniLogistics.Application.AdminCod;
using MiniLogistics.Application.AdminDashboard;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class AdminDashboardAndCodReportServiceTests
{
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _inactiveAdminId = Guid.NewGuid();
    private readonly Guid _shipperId = Guid.NewGuid();

    [Fact]
    public async Task GetAdminDashboard_ForwardsDateProvinceAndActiveShipperCapacity()
    {
        var fromUtc = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
        var toUtc = fromUtc.AddDays(1);
        var repository = new FakeDashboardMetricsRepository();
        var service = new GetAdminDashboardService(CreateIdentityService(), repository, TestClock.Provider);

        var result = await service.GetAsync(new AdminDashboardQuery(
            _adminId,
            fromUtc,
            toUtc,
            " Ho Chi Minh "));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(fromUtc, repository.FromUtc);
        Assert.Equal(toUtc, repository.ToUtc);
        Assert.Equal("Ho Chi Minh", repository.Province);
        var capacity = Assert.Single(repository.ActiveShippers);
        Assert.Equal(_shipperId, capacity.ShipperId);
        Assert.True(capacity.IsAvailableForAssignment);
        Assert.Equal(10, capacity.MaxActiveShipments);
        Assert.Equal(3, result.Value.Shipments.Created);
        Assert.Equal(12.5m, result.Value.Shipments.DeliveryFailedRate);
    }

    [Fact]
    public async Task GetAdminDashboard_RejectsInactiveAdmin()
    {
        var repository = new FakeDashboardMetricsRepository();
        var service = new GetAdminDashboardService(CreateIdentityService(), repository, TestClock.Provider);

        var result = await service.GetAsync(new AdminDashboardQuery(_inactiveAdminId));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task GetAdminCodReport_ForwardsFiltersAndReturnsAggregates()
    {
        var fromUtc = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var toUtc = fromUtc.AddDays(15);
        var repository = new FakeCodReportRepository();
        var service = new GetAdminCodReportService(CreateIdentityService(), repository);

        var result = await service.GetAsync(new AdminCodReportQuery(
            _adminId,
            CodStatus.Collected,
            _shipperId,
            "Ha Noi",
            fromUtc,
            toUtc,
            100_000m,
            500_000m));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.NotNull(repository.Query);
        Assert.Equal(CodStatus.Collected, repository.Query.Status);
        Assert.Equal(_shipperId, repository.Query.ShipperId);
        Assert.Equal("Ha Noi", repository.Query.Province);
        Assert.Equal(100_000m, repository.Query.MinAmount);
        Assert.Equal(500_000m, repository.Query.MaxAmount);
        Assert.Equal(2, result.Value.Summary.CollectedAwaitingSettlementCount);
        Assert.Equal(250_000m, result.Value.Summary.CollectedAwaitingSettlementAmount);
    }

    [Fact]
    public async Task GetAdminCodReport_RejectsInvalidAmountRange()
    {
        var repository = new FakeCodReportRepository();
        var service = new GetAdminCodReportService(CreateIdentityService(), repository);

        var result = await service.GetAsync(new AdminCodReportQuery(
            _adminId,
            MinAmount: 500_000m,
            MaxAmount: 100_000m));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.ValidationFailed", result.Error.Code);
        Assert.Equal(0, repository.CallCount);
    }

    private FakeIdentityService CreateIdentityService()
    {
        return new FakeIdentityService(
            _shipperId,
            [
                new FakeIdentityService.FakeUser(_adminId, true, [nameof(UserRole.Admin)]),
                new FakeIdentityService.FakeUser(_inactiveAdminId, false, [nameof(UserRole.Admin)]),
                new FakeIdentityService.FakeUser(_shipperId, true, [nameof(UserRole.Shipper)])
            ]);
    }

    private sealed class FakeDashboardMetricsRepository : IAdminDashboardMetricsRepository
    {
        public int CallCount { get; private set; }

        public DateTimeOffset FromUtc { get; private set; }

        public DateTimeOffset ToUtc { get; private set; }

        public string? Province { get; private set; }

        public IReadOnlyCollection<ActiveShipperCapacity> ActiveShippers { get; private set; } = [];

        public Task<AdminDashboardRepositoryMetrics> GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            string? province,
            IReadOnlyCollection<ActiveShipperCapacity> activeShippers,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            FromUtc = fromUtc;
            ToUtc = toUtc;
            Province = province;
            ActiveShippers = activeShippers;

            return Task.FromResult(new AdminDashboardRepositoryMetrics(
                new ShipmentOverviewMetrics(3, 1, 1, 8, 12.5m),
                new ShipperOverviewMetrics(1, 1, 0),
                new CodOverviewMetrics(1, 100_000m, 2, 250_000m, 3, 500_000m, "VND"),
                new ShopOverviewMetrics(5, 1),
                new WebhookOverviewMetrics(2, 1)));
        }
    }

    private sealed class FakeCodReportRepository : IAdminCodReportRepository
    {
        public int CallCount { get; private set; }

        public AdminCodReportQuery Query { get; private set; } = null!;

        public Task<AdminCodReportResponse> GetAsync(
            AdminCodReportQuery query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Query = query;

            return Task.FromResult(new AdminCodReportResponse(
                new AdminCodSummary(1, 100_000m, 2, 250_000m, 3, 500_000m, "VND"),
                [
                    new AdminCodTransactionResponse(
                        Guid.NewGuid(),
                        "ML000001",
                        "Receiver",
                        "0911111111",
                        "Ha Noi",
                        query.ShipperId,
                        "Shipper",
                        125_000m,
                        "VND",
                        CodStatus.Collected,
                        ShipmentStatus.Delivered,
                        TestClock.UtcNow,
                        TestClock.UtcNow,
                        query.ShipperId,
                        null,
                        null)
                ],
                [new AdminCodGroupSummary("Shipper", 2, 100_000m, 250_000m, 500_000m, 80m)],
                [new AdminCodGroupSummary("Ha Noi", 2, 100_000m, 250_000m, 500_000m, 80m)],
                [new AdminCodDailySummary(DateOnly.FromDateTime(TestClock.UtcNow.UtcDateTime), 2, 100_000m, 250_000m, 500_000m)]));
        }
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Guid _shipperId;
        private readonly Dictionary<Guid, FakeUser> _users;

        public FakeIdentityService(Guid shipperId, IReadOnlyList<FakeUser> users)
        {
            _shipperId = shipperId;
            _users = users.ToDictionary(user => user.UserId);
        }

        public Task<Result<Guid>> CreateUserAsync(
            string fullName,
            string email,
            string phoneNumber,
            string password,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> AddToRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> CreateInternalUserAsync(
            string fullName,
            string email,
            string phoneNumber,
            string password,
            string role,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetUserActiveStatusAsync(
            Guid userId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetShipperCapacityAsync(
            Guid userId,
            bool isAvailableForAssignment,
            int maxActiveShipments,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var user))
            {
                return Task.FromResult(new IdentityUserRoleCheckResponse(userId, false, false, false));
            }

            return Task.FromResult(new IdentityUserRoleCheckResponse(
                userId,
                true,
                user.IsActive,
                user.Roles.Contains(role)));
        }

        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ActiveShipperResponse> shippers =
            [
                new ActiveShipperResponse(
                    _shipperId,
                    "Active Shipper",
                    "shipper@example.test",
                    "0922222222",
                    true,
                    10)
            ];

            return Task.FromResult(shippers);
        }

        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public sealed record FakeUser(Guid UserId, bool IsActive, HashSet<string> Roles);
    }
}
