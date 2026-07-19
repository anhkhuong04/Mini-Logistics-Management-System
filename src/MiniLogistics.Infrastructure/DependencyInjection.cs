using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminCod;
using MiniLogistics.Application.AdminDashboard;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Application.Shipments;
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
        IConfiguration configuration)
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
        services.AddDataProtection();
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
        services.AddScoped<IApplicationDbTransactionManager, ApplicationDbTransactionManager>();
        services.AddScoped<OutboxMessageRepository>();
        services.AddScoped<IOutboxMessageRepository>(provider => provider.GetRequiredService<OutboxMessageRepository>());
        services.AddScoped<IOutboxWriter>(provider => provider.GetRequiredService<OutboxMessageRepository>());
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<OutboxMessageDispatcher>();
        services.AddHttpClient<WebhookDeliveryDispatcher>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHostedService<OutboxWorker>();
        services.AddHostedService<WebhookDeliveryWorker>();

        return services;
    }
}
