using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminSystemConfiguration;

/// <summary>
/// Defines the application use case contract for Admin System Configuration.
/// </summary>
public interface IAdminSystemConfigurationService
{
    Task<Result<AdminSystemConfigurationResponse>> GetAsync(
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);

    Task<Result<RouteRegionConfigResponse>> UpsertRouteRegionAsync(
        UpsertRouteRegionConfigCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<FeeRuleConfigResponse>> CreateFeeRuleVersionAsync(
        CreateFeeRuleVersionCommand command,
        CancellationToken cancellationToken = default);
}
