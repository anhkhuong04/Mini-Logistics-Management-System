using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniLogistics.Infrastructure;
using MiniLogistics.Infrastructure.Outbox;
using MiniLogistics.Infrastructure.PartnerApi;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class InfrastructureRegistrationProfileTests
{
    [Fact]
    public void OpenApiCompositionOmitsHostedWorkers_WhileRuntimeCompositionRegistersAllWorkers()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=DesignTime;Integrated Security=True"
            })
            .Build();

        var designTimeServices = new ServiceCollection();
        designTimeServices.AddInfrastructure(configuration, registerHostedServices: false);

        var runtimeServices = new ServiceCollection();
        runtimeServices.AddInfrastructure(configuration);
        Type[] workerTypes =
        [
            typeof(OutboxWorker),
            typeof(WebhookDeliveryWorker),
            typeof(PartnerApiRetentionWorker),
            typeof(ShipmentImportWorker)
        ];

        Assert.All(workerTypes, workerType =>
        {
            Assert.DoesNotContain(designTimeServices, descriptor => descriptor.ImplementationType == workerType);
            Assert.Contains(runtimeServices, descriptor => descriptor.ImplementationType == workerType);
        });
    }
}
