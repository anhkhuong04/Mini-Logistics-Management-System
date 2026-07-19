using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application;
using MiniLogistics.Infrastructure;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class LocalDbIntegrationFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"MiniLogisticsIntegration_{Guid.NewGuid():N}";

    public string DemoPartnerApiKey { get; } = $"ml_test_seed_partner_key_{Guid.NewGuid():N}";

    public string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public ServiceProvider ServiceProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] = ConnectionString;
        configuration["Seeding:Enabled"] = "true";
        configuration["Seeding:DemoPartnerApiKey"] = DemoPartnerApiKey;
        configuration["Seeding:DemoAdminPassword"] = CreateSeedPassword("Admin");
        configuration["Seeding:DemoShopPassword"] = CreateSeedPassword("Shop");
        configuration["Seeding:DemoShipperPassword"] = CreateSeedPassword("Shipper");
        configuration["Seeding:DemoOperatorPassword"] = CreateSeedPassword("Operator");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = false
        });

        await using var scope = ServiceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        await dbContext.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }

    public async Task DisposeAsync()
    {
        if (ServiceProvider is not null)
        {
            await using var scope = ServiceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            await dbContext.Database.EnsureDeletedAsync();

            await ServiceProvider.DisposeAsync();
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task ExecuteAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    private static string CreateSeedPassword(string role)
    {
        return $"{role}Seed@{Guid.NewGuid():N}1";
    }
}
