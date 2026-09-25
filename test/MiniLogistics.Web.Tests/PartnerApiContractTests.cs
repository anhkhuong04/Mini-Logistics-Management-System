using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class PartnerApiContractTests
{
    private const string TestApiKey = "ml_test_contract_key_123456";
    private const string ShopTrackingCode = "MLUI202608020001";
    private const string OtherShopTrackingCode = "MLUI202608020002";

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_WhenDependenciesAreAvailable_ReturnsHealthy(string path)
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateShipment_WhenJsonIsMalformed_ReturnsValidationErrorInsteadOfServerError()
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "malformed-json-test");

        using var response = await client.PostAsync(
            "/api/v1/partner/shipments",
            new StringContent("{invalid-json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Application.ValidationFailed",
            document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PartnerApi_WhenRequestBodyExceedsLimit_ReturnsPayloadTooLarge()
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);

        using var response = await client.PostAsync(
            "/api/v1/partner/shipping/quote",
            new StringContent(
                JsonSerializer.Serialize(new { padding = new string('x', 70 * 1024) }),
                Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Request.TooLarge", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task Quote_WhenMissingApiKey_ReturnsStandardUnauthorizedError()
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/partner/shipping/quote", new { });

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("PartnerApi.MissingApiKey", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("error").GetProperty("message").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task PartnerApi_WhenIngressLimitIsExceeded_RejectsBeforeAuthentication()
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();

        for (var index = 0; index < 120; index++)
        {
            using var allowedResponse = await client.PostAsJsonAsync(
                "/api/v1/partner/shipping/quote",
                new { });
            Assert.Equal(HttpStatusCode.Unauthorized, allowedResponse.StatusCode);
        }

        using var rejectedResponse = await client.PostAsJsonAsync(
            "/api/v1/partner/shipping/quote",
            new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.True(rejectedResponse.Headers.Contains("Retry-After"));

        using var document = JsonDocument.Parse(await rejectedResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "PartnerApi.RateLimitExceeded",
            document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TrackShipment_WhenCreatedOutsidePartnerApi_ReturnsShopShipmentWithoutInternalNote()
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);

        var response = await client.GetAsync($"/api/v1/partner/shipments/{ShopTrackingCode}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("externalOrderId").ValueKind);
        var timelineItem = document.RootElement.GetProperty("timeline")[0];
        Assert.Equal("SHIPMENT_PENDING_PICKUP", timelineItem.GetProperty("messageCode").GetString());
        Assert.False(timelineItem.TryGetProperty("note", out _));
        Assert.DoesNotContain("0909123456", document.RootElement.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrackShipment_WhenShipmentBelongsToOtherShop_ReturnsNotFound()
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);

        var response = await client.GetAsync($"/api/v1/partner/shipments/{OtherShopTrackingCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateShipment_WhenInvalidBody_ReturnsValidationErrorAndWritesAudit()
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "contract-invalid-body");

        var response = await client.PostAsJsonAsync("/api/v1/partner/shipments", new
        {
            externalOrderId = "ECOM-CONTRACT-001"
        });

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Application.ValidationFailed", document.RootElement.GetProperty("error").GetProperty("code").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        var audit = await dbContext.PartnerApiRequestAudits.SingleAsync();

        Assert.Equal("ECOM-CONTRACT-001", audit.ExternalOrderId);
        Assert.Equal("contract-invalid-body", audit.IdempotencyKey);
        Assert.Equal(StatusCodes.Status400BadRequest, audit.StatusCode);
        Assert.False(audit.IsSuccess);
        Assert.Equal("Application.ValidationFailed", audit.ErrorCode);
        Assert.NotEmpty(audit.RequestHash);
        Assert.True(audit.DurationMs >= 0);
    }

    [Fact]
    public async Task CreateShipment_WhenAuditStoreFails_PreservesBusinessResponse()
    {
        await using var factory = new PartnerApiWebApplicationFactory(throwAudit: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "audit-store-failure");

        var response = await client.PostAsJsonAsync("/api/v1/partner/shipments", new
        {
            externalOrderId = "ECOM-AUDIT-FAILURE"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Application.ValidationFailed",
            document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateShipment_WhenRateLimitExceeded_ReturnsTooManyRequestsWithRetryAfter()
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 31; i++)
        {
            client.DefaultRequestHeaders.Remove("Idempotency-Key");
            client.DefaultRequestHeaders.Add("Idempotency-Key", $"contract-rate-limit-{i}");
            lastResponse = await client.PostAsJsonAsync("/api/v1/partner/shipments", new
            {
                externalOrderId = $"ECOM-RATE-{i}"
            });
        }

        Assert.NotNull(lastResponse);
        Assert.Equal((HttpStatusCode)429, lastResponse.StatusCode);
        Assert.True(lastResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues));
        Assert.NotEmpty(retryAfterValues);

        var json = await lastResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal("PartnerApi.RateLimitExceeded", document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PartnerApiPreflight_WhenOriginAllowed_ReturnsCorsHeaders()
    {
        const string allowedOrigin = "https://localhost:7195";
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/partner/shipments");
        request.Headers.Add("Origin", allowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization, Content-Type, Idempotency-Key");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins));
        Assert.Equal(allowedOrigin, allowedOrigins.Single());
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Methods", out var allowedMethods));
        Assert.Contains("POST", string.Join(",", allowedMethods), StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Headers", out var allowedHeaders));
        var headerValue = string.Join(",", allowedHeaders);
        Assert.Contains("Authorization", headerValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content-Type", headerValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Idempotency-Key", headerValue, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.TryGetValues("Access-Control-Max-Age", out var maxAgeValues));
        Assert.Equal("600", maxAgeValues.Single());
    }

    [Fact]
    public async Task PartnerApiPreflight_WhenOriginNotAllowed_DoesNotReturnAllowOrigin()
    {
        await using var factory = new PartnerApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/partner/shipments");
        request.Headers.Add("Origin", "https://evil.example.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Access-Control-Allow-Origin", out _));
    }

    [Fact]
    public async Task Quote_WhenShopInactive_ReturnsForbidden()
    {
        await using var factory = new PartnerApiWebApplicationFactory(isShopActive: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);

        var response = await client.PostAsJsonAsync("/api/v1/partner/shipping/quote", new
        {
            deliveryAddress = new
            {
                street = "9 Le Loi",
                ward = "Hoan Kiem",
                province = "Ha Noi",
                country = "Vietnam"
            },
            parcel = new
            {
                weightKg = 1,
                lengthCm = 10,
                widthCm = 10,
                heightCm = 10
            },
            goodsValueAmount = 100000,
            codAmount = 0,
            currency = "VND"
        });

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PartnerApi.ShopInactive", document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Quote_WhenClientLacksQuoteScope_ReturnsForbidden()
    {
        await using var factory = new PartnerApiWebApplicationFactory(scopes: PartnerApiScope.TrackShipment);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);

        var response = await client.PostAsJsonAsync("/api/v1/partner/shipping/quote", new { });
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("PartnerApi.MissingScope", json);
    }

    [Fact]
    public async Task CreateShipment_WhenClientLacksScope_WritesUsageAudit()
    {
        await using var factory = new PartnerApiWebApplicationFactory(scopes: PartnerApiScope.Quote);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);

        var response = await client.PostAsJsonAsync("/api/v1/partner/shipments", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        var audit = await dbContext.PartnerApiRequestAudits.SingleAsync();
        Assert.Equal("/api/v1/partner/shipments", audit.Path);
        Assert.Equal(StatusCodes.Status403Forbidden, audit.StatusCode);
        Assert.False(audit.IsSuccess);
    }

    [Fact]
    public async Task Quote_WhenRequestIpIsNotWhitelisted_ReturnsForbidden()
    {
        await using var factory = new PartnerApiWebApplicationFactory(allowedIpAddresses: ["203.0.113.10"]);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);

        var response = await client.PostAsJsonAsync("/api/v1/partner/shipping/quote", new { });
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("PartnerApi.IpNotAllowed", json);
    }

    [Fact]
    public async Task Quote_WhenUnhandledException_ReturnsSanitizedServerError()
    {
        await using var factory = new PartnerApiWebApplicationFactory(throwQuote: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiKey);

        var response = await client.PostAsJsonAsync("/api/v1/partner/shipping/quote", new
        {
            deliveryAddress = new
            {
                street = "9 Le Loi",
                ward = "Hoan Kiem",
                province = "Ha Noi",
                country = "Vietnam"
            },
            parcel = new
            {
                weightKg = 1,
                lengthCm = 10,
                widthCm = 10,
                heightCm = 10
            },
            goodsValueAmount = 100000,
            codAmount = 0,
            currency = "VND"
        });

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Internal.ServerError", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("error").GetProperty("traceId").GetString()));
        Assert.DoesNotContain(nameof(InvalidOperationException), json);
        Assert.DoesNotContain("quote service failed", json);
    }

    private sealed class PartnerApiWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = "MiniLogisticsContractTests-" + Guid.NewGuid();
        private readonly bool _isShopActive;
        private readonly bool _throwQuote;
        private readonly bool _throwAudit;
        private readonly PartnerApiScope _scopes;
        private readonly IReadOnlyList<string> _allowedIpAddresses;

        public PartnerApiWebApplicationFactory(
            bool isShopActive = true,
            bool throwQuote = false,
            bool throwAudit = false,
            PartnerApiScope scopes = PartnerApiScope.All,
            IReadOnlyList<string>? allowedIpAddresses = null)
        {
            _isShopActive = isShopActive;
            _throwQuote = throwQuote;
            _throwAudit = throwAudit;
            _scopes = scopes;
            _allowedIpAddresses = allowedIpAddresses ?? [];
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MiniLogisticsDbContext>>();
                services.RemoveAll<IHostedService>();
                if (_throwQuote)
                {
                    services.RemoveAll<IPartnerQuoteService>();
                    services.AddScoped<IPartnerQuoteService, ThrowingPartnerQuoteService>();
                }

                if (_throwAudit)
                {
                    services.RemoveAll<IPartnerApiRequestAuditRepository>();
                    services.AddScoped<IPartnerApiRequestAuditRepository, ThrowingRequestAuditRepository>();
                }

                var inMemoryProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();
                services.AddDbContext<MiniLogisticsDbContext>(options =>
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .AddInterceptors(new InMemoryRowVersionInterceptor())
                        .UseInternalServiceProvider(inMemoryProvider));

                using var serviceProvider = services.BuildServiceProvider();
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                var shop = new Shop(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    "Contract Test Shop",
                    new PhoneNumber("0900000001"),
                    new Address("123 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"),
                    TestClock.UtcNow);
                var otherShop = new Shop(
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    "Other Contract Test Shop",
                    new PhoneNumber("0900000002"),
                    new Address("8 Tran Hung Dao", "Hoan Kiem", "Ha Noi"),
                    TestClock.UtcNow);
                var apiClient = new ApiClient(
                    shop.Id,
                    "Contract Test Client",
                    ApiKeyHasher.GetPrefix(TestApiKey),
                    ApiKeyHasher.Hash(TestApiKey),
                    TestClock.UtcNow,
                    _scopes,
                    _allowedIpAddresses);
                if (!_isShopActive)
                {
                    shop.Deactivate(TestClock.UtcNow);
                }

                dbContext.Shops.Add(shop);
                dbContext.Shops.Add(otherShop);
                dbContext.ApiClients.Add(apiClient);
                dbContext.Shipments.Add(CreateTrackingShipment(
                    shop.Id,
                    shop.OwnerUserId,
                    ShopTrackingCode,
                    "Internal note with phone 0909123456."));
                dbContext.Shipments.Add(CreateTrackingShipment(
                    otherShop.Id,
                    otherShop.OwnerUserId,
                    OtherShopTrackingCode,
                    "Other shop internal note."));
                dbContext.SaveChanges();
            });
        }

        private static Shipment CreateTrackingShipment(
            Guid shopId,
            Guid ownerUserId,
            string trackingCode,
            string note)
        {
            return Shipment.Create(
                shopId,
                "Contract Sender",
                new PhoneNumber("0900000010"),
                "Contract Receiver",
                new PhoneNumber("0900000011"),
                new Address("1 Nguyen Hue", "Ben Nghe", "Ho Chi Minh"),
                new Address("2 Le Loi", "Ben Thanh", "Ho Chi Minh"),
                new Weight(1m),
                new ParcelDimensions(10m, 10m, 10m),
                new Weight(1m),
                new Money(100_000m),
                Money.Zero,
                new ShippingFeeBreakdown(new Money(25_000m), Money.Zero, Money.Zero, Money.Zero),
                RouteType.IntraRegion,
                ownerUserId,
                TestClock.UtcNow,
                note,
                new TrackingCode(trackingCode));
        }
    }

    private sealed class ThrowingPartnerQuoteService : IPartnerQuoteService
    {
        public Task<Result<PartnerShippingQuoteResponse>> QuoteAsync(
            PartnerQuoteCommand command,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("quote service failed");
        }
    }

    private sealed class ThrowingRequestAuditRepository : IPartnerApiRequestAuditRepository
    {
        public Task AddAsync(
            PartnerApiRequestAudit audit,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("audit store failed");
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("audit store failed");
        }
    }
}
