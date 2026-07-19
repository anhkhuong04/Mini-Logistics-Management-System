using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Application.Shipments.ImportShipments;

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
        services.AddScoped<IPreviewShipmentImportService>(provider => provider.GetRequiredService<ShipmentImportService>());
        services.AddScoped<IConfirmShipmentImportService>(provider => provider.GetRequiredService<ShipmentImportService>());

        return services;
    }
}
