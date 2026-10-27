using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.ReferenceData;

namespace MiniLogistics.Infrastructure.ReferenceData;

public static class ReferenceDataRegistration
{
    /// <summary>Registers authoritative JSON data at the host-selected path without a Web host dependency.</summary>
    public static IServiceCollection AddAdministrativeDivisionReferenceData(
        this IServiceCollection services, string dataFilePath)
    {
        services.AddSingleton(_ => new VietnamAdministrativeDivisionService(dataFilePath));
        services.AddSingleton<IAdministrativeDivisionService>(provider =>
            provider.GetRequiredService<VietnamAdministrativeDivisionService>());
        services.AddSingleton<IAdministrativeDivisionCatalog>(provider =>
            provider.GetRequiredService<VietnamAdministrativeDivisionService>());
        return services;
    }
}
