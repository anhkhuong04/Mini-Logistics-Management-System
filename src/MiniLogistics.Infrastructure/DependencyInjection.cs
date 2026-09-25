using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminCod;
using MiniLogistics.Application.AdminDashboard;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.CashOnDelivery.GetShipperCodDailySummary;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops;
using MiniLogistics.Application.Shops.Reports;
using MiniLogistics.Application.Shops.Audit;
using MiniLogistics.Application.Shops.Notifications;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.ImportShipments;
using MiniLogistics.Application.Shipments.ProofOfDelivery;
using MiniLogistics.Application.Banners;
using MiniLogistics.Infrastructure.Identity;
using MiniLogistics.Infrastructure.Outbox;
using MiniLogistics.Infrastructure.PartnerApi;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Infrastructure.Persistence.Repositories;

namespace MiniLogistics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool registerHostedServices = true)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<MiniLogisticsDbContext>(options =>
        {
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(MiniLogisticsDbContext).Assembly.FullName));
        });
        services.Configure<SeedingOptions>(
            configuration.GetSection(SeedingOptions.SectionName));
        services
            .AddOptions<WebhookSecurityOptions>()
            .Bind(configuration.GetSection(WebhookSecurityOptions.SectionName))
            .Validate(options => options.AllowedPorts.Length > 0
                && options.AllowedPorts.All(port => port is >= 1 and <= 65535),
                "At least one valid webhook destination port is required.")
            .Validate(options => options.ConnectTimeoutSeconds is >= 1 and <= 30,
                "Webhook connect timeout must be between 1 and 30 seconds.")
            .Validate(options => options.RequestTimeoutSeconds is >= 1 and <= 60,
                "Webhook request timeout must be between 1 and 60 seconds.")
            .Validate(options => options.MaxResponseBodyBytes is >= 256 and <= 65_536,
                "Webhook response body limit must be between 256 and 65536 bytes.")
            .Validate(options => options.MaxResponseHeadersKilobytes is >= 1 and <= 64,
                "Webhook response header limit must be between 1 and 64 KiB.")
            .Validate(options => options.MaxConnectionsPerServer is >= 1 and <= 100,
                "Webhook connection limit must be between 1 and 100.")
            .ValidateOnStart();
        services.Configure<DataProtectionStorageOptions>(
            configuration.GetSection(DataProtectionStorageOptions.SectionName));
        services
            .AddOptions<PartnerApiRetentionOptions>()
            .Bind(configuration.GetSection(PartnerApiRetentionOptions.SectionName))
            .Validate(options => options.IntervalHours is >= 1 and <= 168,
                "Partner API retention interval must be between 1 and 168 hours.")
            .Validate(options => options.SucceededQueueRetentionDays is >= 1 and <= 3650,
                "Succeeded queue retention must be between 1 and 3650 days.")
            .Validate(options => options.RequestAuditRetentionDays is >= 1 and <= 3650,
                "Request audit retention must be between 1 and 3650 days.")
            .Validate(options => options.CredentialAuditRetentionDays is >= 1 and <= 3650,
                "Credential audit retention must be between 1 and 3650 days.")
            .Validate(options => options.BatchSize is >= 10 and <= 10_000,
                "Partner API retention batch size must be between 10 and 10000.")
            .ValidateOnStart();
        services.AddMemoryCache();

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = PasswordPolicy.RequiredLength;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequiredUniqueChars = PasswordPolicy.RequiredUniqueChars;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<MiniLogisticsDbContext>()
            .AddDefaultTokenProviders();
        ConfigureDataProtection(services, configuration);
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
            options.LogoutPath = "/auth/logout";
        });

        services.AddScoped<ShipmentRepository>();
        services.AddScoped<IShipmentRepository>(provider => provider.GetRequiredService<ShipmentRepository>());
        services.AddScoped<IShipmentReadRepository>(provider => provider.GetRequiredService<ShipmentRepository>());
        services.AddScoped<IShipmentWriteRepository>(provider => provider.GetRequiredService<ShipmentRepository>());
        services.AddScoped<FeeRuleRepository>();
        services.AddScoped<FeeRuleCache>();
        services.AddScoped<IFeeRuleRepository>(provider => provider.GetRequiredService<FeeRuleCache>());
        services.AddScoped<IFeeRuleCache>(provider => provider.GetRequiredService<FeeRuleCache>());
        services.AddScoped<IFeeConfigurationRepository, FeeConfigurationRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<HubRepository>();
        services.AddScoped<IHubRepository, CachedHubRepository>();
        services.AddScoped<RouteRegionConfigRepository>();
        services.AddScoped<IRouteRegionConfigRepository, CachedRouteRegionConfigRepository>();
        services.AddScoped<IRouteRegionConfigSource>(provider => provider.GetRequiredService<IRouteRegionConfigRepository>());
        services.AddScoped<IShipperWorkingAreaRepository, ShipperWorkingAreaRepository>();
        services.AddScoped<ICodTransactionRepository, CodTransactionRepository>();
        services.AddScoped<IDeliveryProofRepository, DeliveryProofRepository>();
        services.AddScoped<IShipperCodDailySummaryRepository, ShipperCodDailySummaryRepository>();
        services.AddScoped<IApiClientRepository, ApiClientRepository>();
        services.AddScoped<IExternalShipmentReferenceRepository, ExternalShipmentReferenceRepository>();
        services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IPartnerApiRequestAuditRepository, PartnerApiRequestAuditRepository>();
        services.AddScoped<IPartnerApiCredentialAuditRepository, PartnerApiCredentialAuditRepository>();
        services.AddScoped<IIntegrationManagementScopeRepository, IntegrationManagementScopeRepository>();
        services.AddScoped<IAdminAuditLogRepository, AdminAuditLogRepository>();
        services.AddScoped<IAdminAuditService, AdminAuditService>();
        services.AddScoped<IAdminDashboardMetricsRepository, AdminDashboardMetricsRepository>();
        services.AddScoped<IAdminCodReportRepository, AdminCodReportRepository>();
        services.AddScoped<IShopReportingRepository, ShopReportingRepository>();
        services.AddScoped<IShipmentImportBatchRepository, ShipmentImportBatchRepository>();
        services.AddScoped<IShopAuditLogRepository, ShopAuditLogRepository>();
        services.AddScoped<IShopNotificationRepository, ShopNotificationRepository>();
        services.AddScoped<IShopStaffMembershipRepository, ShopStaffMembershipRepository>();
        services.AddScoped<IBannerRepository, BannerRepository>();
        services.AddScoped<IApplicationDbTransactionManager, ApplicationDbTransactionManager>();
        services.AddScoped<OutboxMessageRepository>();
        services.AddScoped<IOutboxMessageRepository>(provider => provider.GetRequiredService<OutboxMessageRepository>());
        services.AddScoped<IOutboxWriter>(provider => provider.GetRequiredService<OutboxMessageRepository>());
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserPasswordService, UserPasswordService>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IWebhookDnsResolver, SystemWebhookDnsResolver>();
        services.AddSingleton<IWebhookUrlPolicy, WebhookUrlPolicy>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<OutboxMessageDispatcher>();
        services.AddScoped<PartnerApiRetentionService>();
        services.AddHttpClient<WebhookDeliveryDispatcher>(client =>
        {
            var requestTimeout = configuration.GetValue<int?>(
                $"{WebhookSecurityOptions.SectionName}:RequestTimeoutSeconds") ?? 10;
            client.Timeout = TimeSpan.FromSeconds(requestTimeout);
        })
        .ConfigurePrimaryHttpMessageHandler(serviceProvider => WebhookHttpMessageHandler.Create(
            serviceProvider.GetRequiredService<IWebhookDnsResolver>(),
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebhookSecurityOptions>>()));
        if (registerHostedServices)
        {
            services.AddHostedService<OutboxWorker>();
            services.AddHostedService<WebhookDeliveryWorker>();
            services.AddHostedService<PartnerApiRetentionWorker>();
            services.AddHostedService<ShipmentImportWorker>();
        }

        return services;
    }

    private static void ConfigureDataProtection(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(DataProtectionStorageOptions.SectionName)
            .Get<DataProtectionStorageOptions>() ?? new DataProtectionStorageOptions();
        if (string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            throw new InvalidOperationException("DataProtection:ApplicationName is required.");
        }

        if (options.KeyLifetimeDays < 7)
        {
            throw new InvalidOperationException("DataProtection:KeyLifetimeDays must be at least 7.");
        }

        var builder = services
            .AddDataProtection()
            .SetApplicationName(options.ApplicationName)
            .SetDefaultKeyLifetime(TimeSpan.FromDays(options.KeyLifetimeDays));

        if (!string.IsNullOrWhiteSpace(options.KeysPath))
        {
            Directory.CreateDirectory(options.KeysPath);
            builder.PersistKeysToFileSystem(new DirectoryInfo(options.KeysPath));
        }

        if (!string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                options.CertificatePath,
                options.CertificatePassword,
                X509KeyStorageFlags.EphemeralKeySet);
            builder.ProtectKeysWithCertificate(certificate);
            services.AddSingleton(certificate);
        }
    }
}
