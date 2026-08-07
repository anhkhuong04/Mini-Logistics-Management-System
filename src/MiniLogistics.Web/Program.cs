using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MiniLogistics.Application;
using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Infrastructure;
using MiniLogistics.Infrastructure.Health;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Web.Components;
using MiniLogistics.Web.Endpoints;
using MiniLogistics.Web.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
const string PartnerApiCorsPolicy = "PartnerApiPolicy";
var isOpenApiDocumentGeneration = string.Equals(
    Assembly.GetEntryAssembly()?.GetName().Name,
    "GetDocument.Insider",
    StringComparison.Ordinal);
if (isOpenApiDocumentGeneration
    && string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] =
            "Server=(localdb)\\MSSQLLocalDB;Database=MiniLogisticsOpenApi;Trusted_Connection=True;TrustServerCertificate=True"
    });
}

// Add services to the container.
builder.Services.AddSingleton(TimeProvider.System);
var partnerApiEnvironment = builder.Configuration["PartnerApi:Environment"] ?? "Sandbox";
builder.Services.AddSingleton<IApiCredentialPolicy>(
    new EnvironmentApiCredentialPolicy(partnerApiEnvironment));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var healthChecks = builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<SqlServerHealthCheck>("sql-server", tags: ["ready"])
    .AddCheck<DataProtectionHealthCheck>("data-protection", tags: ["ready"]);
var trustedProxyOptions = builder.Configuration
    .GetSection(TrustedProxyOptions.SectionName)
    .Get<TrustedProxyOptions>() ?? new TrustedProxyOptions();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = trustedProxyOptions.ForwardLimit;
    options.RequireHeaderSymmetry = true;
    options.ForwardedForHeaderName = trustedProxyOptions.ForwardedForHeaderName;
    options.ForwardedProtoHeaderName = trustedProxyOptions.ForwardedProtoHeaderName;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();

    foreach (var proxy in trustedProxyOptions.KnownProxies)
    {
        if (!IPAddress.TryParse(proxy, out var address))
        {
            throw new InvalidOperationException($"TrustedProxy:KnownProxies contains invalid IP address '{proxy}'.");
        }

        options.KnownProxies.Add(address);
    }

    foreach (var networkValue in trustedProxyOptions.KnownNetworks)
    {
        if (!System.Net.IPNetwork.TryParse(networkValue, out var network))
        {
            throw new InvalidOperationException($"TrustedProxy:KnownNetworks contains invalid CIDR '{networkValue}'.");
        }

        options.KnownIPNetworks.Add(network);
    }
});
var partnerApiAllowedOrigins = builder.Configuration
    .GetSection("Cors:PartnerApi:AllowedOrigins")
    .Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(PartnerApiCorsPolicy, policy =>
    {
        policy
            .WithOrigins(partnerApiAllowedOrigins)
            .WithMethods("GET", "POST")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});
builder.Services.AddExceptionHandler<PartnerApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description => description.GroupName == "v1";
    options.AddDocumentTransformer<PartnerApiOpenApiTransformer>();
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        OperationPermissions.ShipmentView,
        policy => policy.RequireRole("Admin", "Operator"));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<VietnamAdministrativeDivisionService>();
builder.Services.AddSingleton<MiniLogistics.Application.Common.IAdministrativeDivisionService>(provider =>
    provider.GetRequiredService<VietnamAdministrativeDivisionService>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.Configure<PublicTrackingRateLimitOptions>(
    builder.Configuration.GetSection(PublicTrackingRateLimitOptions.SectionName));
builder.Services.AddSingleton<IPublicTrackingRateLimiter, DistributedCachePublicTrackingRateLimiter>();
builder.Services
    .AddOptions<PartnerApiRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(PartnerApiRateLimitOptions.SectionName))
    .Validate(options =>
        options.QuoteLimitPerMinute > 0
        && options.CreateShipmentLimitPerMinute > 0
        && options.TrackingLimitPerMinute > 0
        && options.CancelShipmentLimitPerMinute > 0,
        "All Partner API rate limits must be greater than zero.")
    .Validate(options => options.StoreTimeoutMilliseconds is >= 50 and <= 5000,
        "Partner API rate limit store timeout must be between 50 and 5000 milliseconds.")
    .Validate(options => options.StoreFailureThreshold is >= 1 and <= 100,
        "Partner API rate limit store failure threshold must be between 1 and 100.")
    .Validate(options => options.CircuitBreakSeconds is >= 1 and <= 300,
        "Partner API rate limit circuit break duration must be between 1 and 300 seconds.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.KeyPrefix),
        "Partner API rate limit key prefix is required.")
    .ValidateOnStart();
builder.Services.Configure<ShopUiActionRateLimitOptions>(
    builder.Configuration.GetSection(ShopUiActionRateLimitOptions.SectionName));
builder.Services.AddSingleton<IShopUiActionRateLimiter, DistributedCacheShopUiActionRateLimiter>();
var partnerRateLimitMode = builder.Configuration.GetValue<string>(
    $"{PartnerApiRateLimitOptions.SectionName}:Mode") ?? "Memory";
if (string.Equals(partnerRateLimitMode, "Redis", StringComparison.OrdinalIgnoreCase))
{
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Connection string 'Redis' is required for Partner API Redis rate limiting.");
    var redisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
    redisConfiguration.AbortOnConnectFail = false;
    redisConfiguration.ConnectRetry = 2;
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfiguration));
    builder.Services.AddSingleton<IRedisRateLimitStore, RedisRateLimitStore>();
    builder.Services.AddSingleton<IPartnerApiRateLimiter, RedisPartnerApiRateLimiter>();
    healthChecks.AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
}
else if (string.Equals(partnerRateLimitMode, "Memory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IPartnerApiRateLimiter, InMemoryPartnerApiRateLimiter>();
}
else
{
    throw new InvalidOperationException(
        "PartnerApi:RateLimiting:Mode must be either 'Memory' or 'Redis'. The legacy non-atomic 'Distributed' mode is not supported.");
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

var shouldMigrate = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
var shouldSeed = args.Contains("--seed", StringComparer.OrdinalIgnoreCase);
if (shouldMigrate || shouldSeed)
{
    await RunDatabaseCommandsAsync(app, shouldMigrate, shouldSeed);
    return;
}

if (app.Environment.IsProduction() && !isOpenApiDocumentGeneration)
{
    ProductionConfigurationGuard.Validate(
        builder.Configuration,
        trustedProxyOptions,
        partnerRateLimitMode);
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/v1/partner"),
    partnerApi => partnerApi.Use(async (context, next) =>
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
        using var logScope = app.Logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = context.TraceIdentifier,
            ["TraceId"] = Activity.Current?.TraceId.ToString()
        });
        try
        {
            await next(context);
        }
        finally
        {
            var route = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "/api/v1/partner";
            PartnerApiTelemetry.RecordRequest(
                context.Request.Method,
                route,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds);
        }
    }));
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    apiApp => apiApp.UseExceptionHandler());
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthenticationEndpoints();
app.MapShopShipmentFileEndpoints();
app.MapPartnerApiEndpoints(PartnerApiCorsPolicy);
if (!app.Environment.IsProduction())
{
    app.MapOpenApi("/openapi/{documentName}.json");
}
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponseAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task RunDatabaseCommandsAsync(
    WebApplication app,
    bool shouldMigrate,
    bool shouldSeed)
{
    await using var scope = app.Services.CreateAsyncScope();

    if (shouldMigrate)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (shouldSeed)
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return JsonSerializer.SerializeAsync(
        context.Response.Body,
        new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    durationMs = entry.Value.Duration.TotalMilliseconds,
                    data = entry.Value.Data
                })
        });
}

public partial class Program;
