using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class PartnerApiContractTests
{
    private const string TestApiKey = "ml_test_contract_key_123456";

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

        public PartnerApiWebApplicationFactory(bool isShopActive = true, bool throwQuote = false)
        {
            _isShopActive = isShopActive;
            _throwQuote = throwQuote;
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

                var inMemoryProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();
                services.AddDbContext<MiniLogisticsDbContext>(options =>
                    options
                        .UseInMemoryDatabase(_databaseName)
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
                var apiClient = new ApiClient(
                    shop.Id,
                    "Contract Test Client",
                    ApiKeyHasher.GetPrefix(TestApiKey),
                    ApiKeyHasher.Hash(TestApiKey),
                    TestClock.UtcNow);
                if (!_isShopActive)
                {
                    shop.Deactivate(TestClock.UtcNow);
                }

                dbContext.Shops.Add(shop);
                dbContext.ApiClients.Add(apiClient);
                dbContext.SaveChanges();
            });
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
}
