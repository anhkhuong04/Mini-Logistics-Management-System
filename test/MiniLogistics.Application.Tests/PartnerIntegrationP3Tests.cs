using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class PartnerIntegrationP3Tests
{
    private readonly Guid _adminId = Guid.NewGuid();

    [Fact]
    public async Task Dashboard_AdminWithoutConfiguredScopes_RemainsBackwardCompatible()
    {
        var hcmShop = CreateShop("HCM Shop", "Ho Chi Minh");
        var hnShop = CreateShop("HN Shop", "Ha Noi");
        var service = CreateService(
            [hcmShop, hnShop],
            [],
            new FakeIntegrationScopeRepository(anyScopeConfigured: false, []));

        var result = await service.GetDashboardAsync(_adminId);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.False(result.Value.GranularPermissionEnabled);
        Assert.Equal(2, result.Value.Shops.Count);
    }

    [Fact]
    public async Task CreateApiClient_AdminWithProvinceScope_CannotManageOutOfScopeShop()
    {
        var hcmShop = CreateShop("HCM Shop", "Ho Chi Minh");
        var hnShop = CreateShop("HN Shop", "Ha Noi");
        var service = CreateService(
            [hcmShop, hnShop],
            [],
            new FakeIntegrationScopeRepository(
                anyScopeConfigured: true,
                [new IntegrationManagementScope(_adminId, province: "Ho Chi Minh")]));

        var result = await service.CreateApiClientAsync(new CreatePartnerApiClientCommand(
            _adminId,
            hnShop.Id,
            "Out of scope app"));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Dashboard_ComputesWebhookMetricsWithoutExposingSecrets()
    {
        var shop = CreateShop("HCM Shop", "Ho Chi Minh");
        var apiClient = new ApiClient(shop.Id, "Storefront", "ml_live_abcd", "hashed-key");
        var succeeded = CreateDelivery(apiClient.Id);
        succeeded.MarkSucceeded(200, DateTimeOffset.UtcNow, durationMs: 120);
        var failed = CreateDelivery(apiClient.Id);
        failed.MarkFailed(500, "Server error", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), durationMs: 400);
        var service = CreateService(
            [shop],
            [apiClient],
            new FakeIntegrationScopeRepository(anyScopeConfigured: false, []),
            [succeeded, failed]);

        var result = await service.GetDashboardAsync(_adminId);

        Assert.True(result.IsSuccess, result.Error.Description);
        var client = Assert.Single(result.Value.ApiClients);
        Assert.Equal(2, client.WebhookMetrics.TotalDeliveries);
        Assert.Equal(1, client.WebhookMetrics.SucceededDeliveries);
        Assert.Equal(1, client.WebhookMetrics.FailedDeliveries);
        Assert.Equal(1, client.WebhookMetrics.PendingRetryDeliveries);
        Assert.Equal(50m, client.WebhookMetrics.SuccessRate);
        Assert.Equal(260m, client.WebhookMetrics.AverageLatencyMs);
        Assert.All(
            client.RecentDeliveries.Select(delivery => delivery.LastError ?? string.Empty),
            error => Assert.DoesNotContain("hashed-key", error));
    }

    private PartnerIntegrationManagementService CreateService(
        IReadOnlyList<Shop> shops,
        IReadOnlyList<ApiClient> apiClients,
        IIntegrationManagementScopeRepository scopeRepository,
        IReadOnlyList<WebhookDelivery>? deliveries = null)
    {
        return new PartnerIntegrationManagementService(
            new FakeIdentityService(_adminId),
            new FakeShopRepository(shops),
            new FakeApiClientRepository(apiClients),
            new FakeWebhookEndpointRepository(),
            new FakeWebhookDeliveryRepository(deliveries ?? []),
            new FakePartnerApiCredentialAuditRepository(),
            new FakeSecretProtector(),
            NullAdminAuditService.Instance,
            scopeRepository);
    }

    private static Shop CreateShop(string name, string province)
    {
        return new Shop(
            Guid.NewGuid(),
            name,
            new PhoneNumber("0900000000"),
            new Address("1 Test", "Ward", province));
    }

    private static WebhookDelivery CreateDelivery(Guid apiClientId)
    {
        return new WebhookDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            apiClientId,
            WebhookEventTypes.ShipmentStatusChanged,
            Guid.NewGuid(),
            "{}");
    }

    private sealed class FakeIntegrationScopeRepository : IIntegrationManagementScopeRepository
    {
        private readonly bool _anyScopeConfigured;
        private readonly IReadOnlyList<IntegrationManagementScope> _scopes;

        public FakeIntegrationScopeRepository(
            bool anyScopeConfigured,
            IReadOnlyList<IntegrationManagementScope> scopes)
        {
            _anyScopeConfigured = anyScopeConfigured;
            _scopes = scopes;
        }

        public Task<bool> AnyActiveScopeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_anyScopeConfigured);
        }

        public Task<IReadOnlyList<IntegrationManagementScope>> GetActiveByActorUserIdAsync(
            Guid actorUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IntegrationManagementScope>>(_scopes
                .Where(scope => scope.ActorUserId == actorUserId && scope.IsActive)
                .ToList());
        }
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Guid _adminUserId;

        public FakeIdentityService(Guid adminUserId)
        {
            _adminUserId = adminUserId;
        }

        public Task<Result<Guid>> CreateUserAsync(string fullName, string email, string phoneNumber, string password, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> AddToRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> CreateInternalUserAsync(string fullName, string email, string phoneNumber, string password, string role, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetShipperCapacityAsync(Guid userId, bool isAvailableForAssignment, int maxActiveShipments, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IdentityUserRoleCheckResponse(
                userId,
                userId == _adminUserId,
                userId == _adminUserId,
                userId == _adminUserId && role == nameof(UserRole.Admin)));
        }

        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeShopRepository : IShopRepository
    {
        private readonly List<Shop> _shops;

        public FakeShopRepository(IReadOnlyList<Shop> shops)
        {
            _shops = shops.ToList();
        }

        public Task<Shop?> GetByIdAsync(Guid shopId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.FirstOrDefault(shop => shop.Id == shopId));
        }

        public Task<Shop?> GetByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.FirstOrDefault(shop => shop.OwnerUserId == ownerUserId));
        }

        public Task<IReadOnlyList<Shop>> GetAllByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shop>>(_shops.Where(shop => shop.OwnerUserId == ownerUserId).ToList());
        }

        public Task<IReadOnlyList<Shop>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shop>>(_shops);
        }

        public Task<bool> ExistsByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.Any(shop => shop.OwnerUserId == ownerUserId));
        }

        public Task AddAsync(Shop shop, CancellationToken cancellationToken = default)
        {
            _shops.Add(shop);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeApiClientRepository : IApiClientRepository
    {
        private readonly List<ApiClient> _clients;

        public FakeApiClientRepository(IReadOnlyList<ApiClient> clients)
        {
            _clients = clients.ToList();
        }

        public Task<ApiClient?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_clients.FirstOrDefault(client => client.ApiKeyHash == apiKeyHash));
        }

        public Task<ApiClient?> GetByIdAsync(Guid apiClientId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_clients.FirstOrDefault(client => client.Id == apiClientId));
        }

        public Task<IReadOnlyList<ApiClient>> GetByShopIdsAsync(IReadOnlyCollection<Guid> shopIds, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApiClient>>(_clients.Where(client => shopIds.Contains(client.ShopId)).ToList());
        }

        public Task AddAsync(ApiClient apiClient, CancellationToken cancellationToken = default)
        {
            _clients.Add(apiClient);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebhookEndpointRepository : IWebhookEndpointRepository
    {
        public Task<IReadOnlyList<WebhookEndpoint>> GetActiveByApiClientIdAsync(Guid apiClientId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookEndpoint>>([]);
        }

        public Task<IReadOnlyList<WebhookEndpoint>> GetByApiClientIdsAsync(IReadOnlyCollection<Guid> apiClientIds, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookEndpoint>>([]);
        }

        public Task<WebhookEndpoint?> GetByIdAsync(Guid webhookEndpointId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WebhookEndpoint?>(null);
        }

        public Task<WebhookEndpoint?> GetLatestByApiClientIdAsync(Guid apiClientId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WebhookEndpoint?>(null);
        }

        public Task AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebhookDeliveryRepository : IWebhookDeliveryRepository
    {
        private readonly IReadOnlyList<WebhookDelivery> _deliveries;

        public FakeWebhookDeliveryRepository(IReadOnlyList<WebhookDelivery> deliveries)
        {
            _deliveries = deliveries;
        }

        public Task<bool> ExistsAsync(Guid deliveryId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_deliveries.Any(delivery => delivery.Id == deliveryId));
        }

        public Task<IReadOnlyList<WebhookDelivery>> GetDueAsync(DateTimeOffset dueAtUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookDelivery>>([]);
        }

        public Task<IReadOnlyList<WebhookDelivery>> GetRecentByApiClientIdsAsync(IReadOnlyCollection<Guid> apiClientIds, int takePerClient, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookDelivery>>(_deliveries
                .Where(delivery => apiClientIds.Contains(delivery.ApiClientId))
                .Take(takePerClient)
                .ToList());
        }

        public Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakePartnerApiCredentialAuditRepository : IPartnerApiCredentialAuditRepository
    {
        public Task<IReadOnlyList<PartnerApiCredentialAudit>> GetRecentByApiClientIdsAsync(IReadOnlyCollection<Guid> apiClientIds, int takePerClient, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PartnerApiCredentialAudit>>([]);
        }

        public Task AddAsync(PartnerApiCredentialAudit audit, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext)
        {
            return plaintext;
        }

        public string Unprotect(string protectedValue)
        {
            return protectedValue;
        }

        public bool IsProtected(string value)
        {
            return true;
        }
    }
}
