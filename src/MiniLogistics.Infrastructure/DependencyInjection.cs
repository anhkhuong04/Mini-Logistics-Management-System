using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shops;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Infrastructure.Identity;
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

        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IFeeRuleRepository, FeeRuleRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<ICodTransactionRepository, CodTransactionRepository>();
        services.AddScoped<IApiClientRepository, ApiClientRepository>();
        services.AddScoped<IExternalShipmentReferenceRepository, ExternalShipmentReferenceRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
