using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.PartnerApi;
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

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireDigit = true;
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

        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IFeeRuleRepository, FeeRuleRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IHubRepository, HubRepository>();
        services.AddScoped<IShipperWorkingAreaRepository, ShipperWorkingAreaRepository>();
        services.AddScoped<ICodTransactionRepository, CodTransactionRepository>();
        services.AddScoped<IApiClientRepository, ApiClientRepository>();
        services.AddScoped<IExternalShipmentReferenceRepository, ExternalShipmentReferenceRepository>();
        services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IPartnerApiRequestAuditRepository, PartnerApiRequestAuditRepository>();
        services.AddScoped<IPartnerApiCredentialAuditRepository, PartnerApiCredentialAuditRepository>();
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
