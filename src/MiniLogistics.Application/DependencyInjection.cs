using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Application.Shipments.ImportShipments;
using MiniLogistics.Application.Common;

namespace MiniLogistics.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.Scan(scan => scan
            .FromAssemblies(typeof(DependencyInjection).Assembly)
            .AddClasses(classes => classes.Where(type =>
                type.Name.EndsWith("Service", StringComparison.Ordinal)
                && type.GetConstructors().Length > 0
                && type != typeof(ShipmentImportService)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddScoped<PartnerCredentialAuditWriter>();
        services.AddScoped<PartnerIntegrationDashboardBuilder>();
        services.AddScoped<IWebhookEventPublisher, WebhookEventPublisher>();
        services.AddScoped<IShipmentAssignmentSelector, ShipmentAssignmentSelector>();
        services.AddScoped<ShipmentImportService>();
        services.AddScoped<IPreviewShipmentImportService>(provider => (IPreviewShipmentImportService)OperationScopedServiceProxy.Create(
            typeof(IPreviewShipmentImportService),
            typeof(ShipmentImportService),
            provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddScoped<IConfirmShipmentImportService>(provider => (IConfirmShipmentImportService)OperationScopedServiceProxy.Create(
            typeof(IConfirmShipmentImportService),
            typeof(ShipmentImportService),
            provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddScoped<ShipmentImportBatchProcessor>();

        RegisterOperationScopedServices(services);

        return services;
    }

    private static void RegisterOperationScopedServices(IServiceCollection services)
    {
        var applicationAssembly = typeof(DependencyInjection).Assembly;
        var serviceDescriptors = services
            .Where(descriptor => descriptor.ServiceType.IsInterface
                && descriptor.ImplementationType?.Assembly == applicationAssembly
                && descriptor.ImplementationType.Name.EndsWith("Service", StringComparison.Ordinal))
            .ToArray();

        foreach (var descriptor in serviceDescriptors)
        {
            var implementationType = descriptor.ImplementationType!;
            services.TryAddScoped(implementationType);
            services.Remove(descriptor);
            services.AddScoped(descriptor.ServiceType, provider => OperationScopedServiceProxy.Create(
                descriptor.ServiceType,
                implementationType,
                provider.GetRequiredService<IServiceScopeFactory>()));
        }
    }
}
