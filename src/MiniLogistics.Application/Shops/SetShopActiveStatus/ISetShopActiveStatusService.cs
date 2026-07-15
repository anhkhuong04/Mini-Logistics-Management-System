using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.SetShopActiveStatus;

public interface ISetShopActiveStatusService
{
    Task<Result> SetAsync(
        SetShopActiveStatusCommand command,
        CancellationToken cancellationToken = default);
}
