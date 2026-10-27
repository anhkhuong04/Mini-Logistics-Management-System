using MiniLogistics.Infrastructure.ReferenceData;
using MiniLogistics.Infrastructure.Persistence.Seeding;
using MiniLogistics.Infrastructure.RateLimiting;
using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniLogistics.Application;
using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Infrastructure;
using MiniLogistics.Infrastructure.Health;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Web.Endpoints;
using MiniLogistics.Web.Services;
using StackExchange.Redis;
using MiniLogistics.Web.Endpoints.Browser;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
const string PartnerApiCorsPolicy = "PartnerApiPolicy";
const string PartnerApiIngressRateLimitPolicy = "PartnerApiIngress";
const long PartnerApiMaxRequestBodySize = 64 * 1024;
const int PartnerApiIngressRequestsPerMinute = 120;
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
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("vi-VN"),
        new CultureInfo("en-US")
    };

    options.DefaultRequestCulture = new RequestCulture("vi-VN");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = [new CookieRequestCultureProvider()];
});
var partnerApiEnvironment = builder.Configuration["PartnerApi:Environment"] ?? "Sandbox";
builder.Services.AddSingleton<IApiCredentialPolicy>(
    new EnvironmentApiCredentialPolicy(partnerApiEnvironment));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration,
    registerHostedServices: !isOpenApiDocumentGeneration && !builder.Environment.IsEnvironment("BrowserTests"));
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
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(PartnerApiIngressRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = PartnerApiIngressRequestsPerMinute,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        if (BrowserApiEndpoints.IsBrowserPath(context.HttpContext.Request.Path))
        {
            await BrowserApiResultMapper.Problem(context.HttpContext, "Browser.RateLimitExceeded", 429)
                .ExecuteAsync(context.HttpContext);
            return;
        }
        await context.HttpContext.Response.WriteAsJsonAsync(
            new PartnerApiErrorResponse(new PartnerApiError(
                "PartnerApi.RateLimitExceeded",
                "Too many requests. Please retry later.",
                context.HttpContext.TraceIdentifier)),
            cancellationToken);
    };
});
builder.Services.AddExceptionHandler<PartnerApiExceptionHandler>();
builder.Services.AddExceptionHandler<BrowserApiExceptionHandler>();
builder.Services.AddScoped<BrowserSessionReader>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnValidatePrincipal = async context =>
    {
        await Microsoft.AspNetCore.Identity.SecurityStampValidator.ValidatePrincipalAsync(context);
        if (BrowserApiEndpoints.IsBrowserPath(context.Request.Path) && context.Principal?.Identity?.IsAuthenticated == true)
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MiniLogistics.Infrastructure.Identity.ApplicationUser>>();
            var user = await users.GetUserAsync(context.Principal);
            if (user is null)
                context.RejectPrincipal();
            else
                context.ReplacePrincipal(await context.HttpContext.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Identity.SignInManager<MiniLogistics.Infrastructure.Identity.ApplicationUser>>().CreateUserPrincipalAsync(user));
        }
    };
    options.Events.OnRedirectToLogin = context => BrowserApiEndpoints.IsBrowserPath(context.Request.Path)
        ? BrowserApiResultMapper.Problem(context.HttpContext, "Browser.Unauthenticated", 401).ExecuteAsync(context.HttpContext)
        : LegacyRedirect(context);
    options.Events.OnRedirectToAccessDenied = context => BrowserApiEndpoints.IsBrowserPath(context.Request.Path)
        ? BrowserApiResultMapper.Problem(context.HttpContext, "Application.Forbidden", 403).ExecuteAsync(context.HttpContext)
        : LegacyRedirect(context);
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description => description.GroupName == "v1";
    options.AddDocumentTransformer<PartnerApiOpenApiTransformer>();
});
builder.Services.AddOpenApi("browser-v1", options =>
{
    options.ShouldInclude = description => description.GroupName is "browser-v1" or "public-v1";
    options.AddSchemaTransformer<BrowserApiOpenApiTransformer>();
    options.AddDocumentTransformer<BrowserApiDocumentTransformer>();
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        OperationPermissions.ShipmentView,
        policy => policy.RequireRole("Admin", "Operator"));
});
builder.Services.AddAdministrativeDivisionReferenceData(Path.Combine(
    builder.Environment.WebRootPath, "data", "vietnam-administrative-divisions.json"));
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
    builder.Services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfiguration));
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

builder.Services.AddBrowserSpa(builder.Configuration, builder.Environment);

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
app.UseRequestLocalization();
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/v1/partner"),
    partnerApi =>
    {
        partnerApi.Use(async (context, next) =>
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
        });

        partnerApi.Use(async (context, next) =>
        {
            var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (maxRequestBodySizeFeature is { IsReadOnly: false })
            {
                maxRequestBodySizeFeature.MaxRequestBodySize = PartnerApiMaxRequestBodySize;
            }

            if (context.Request.ContentLength > PartnerApiMaxRequestBodySize)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                await context.Response.WriteAsJsonAsync(
                    new PartnerApiErrorResponse(new PartnerApiError(
                        "Request.TooLarge",
                        $"Request body must not exceed {PartnerApiMaxRequestBodySize / 1024} KB.",
                        context.TraceIdentifier)),
                    context.RequestAborted);
                return;
            }

            await next(context);
        });
    });
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    apiApp => apiApp.UseExceptionHandler());
app.UseWhen(
    context => BrowserApiEndpoints.IsBrowserPath(context.Request.Path),
    browserApp => browserApp.UseStatusCodePages(async statusContext =>
    {
        var context = statusContext.HttpContext;
        await BrowserApiResultMapper.Problem(context, context.Response.StatusCode == 404
            ? "Application.NotFound" : "Browser.HttpError", context.Response.StatusCode).ExecuteAsync(context);
    }));
app.UseHttpsRedirection();
app.UseBrowserSpaAssets();
app.UseBannerImages();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/_blazor") || context.Request.Path.StartsWithSegments("/_framework"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next(context);
});

app.UseRouting();
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapBrowserSpa();
app.MapBrowserApiEndpoints();
app.MapLocalizationEndpoints();
app.MapAuthenticationEndpoints();
app.MapShopShipmentFileEndpoints();
app.MapPartnerApiEndpoints(PartnerApiCorsPolicy, PartnerApiIngressRateLimitPolicy);
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

// Pre-warm the EF Core model to avoid ObjectDisposedException in background workers
// caused by EF Core's internal service provider caching the scoped ApplicationServiceProvider
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
    _ = dbContext.Model; // This forces the model to be built and cached
}

app.Run();

static Task LegacyRedirect(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context)
{
    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

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
