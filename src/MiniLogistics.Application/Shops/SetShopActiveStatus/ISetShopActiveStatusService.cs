using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.SetShopActiveStatus;

/// <summary>
/// Defines the application use case contract for Set Shop Active Status.
/// </summary>
public interface ISetShopActiveStatusService
{
    Task<Result> SetAsync(
        SetShopActiveStatusCommand command,
        CancellationToken cancellationToken = default);
}
