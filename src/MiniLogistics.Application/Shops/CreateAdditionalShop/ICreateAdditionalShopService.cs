using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.CreateAdditionalShop;

public interface ICreateAdditionalShopService
{
    Task<Result<CreateAdditionalShopResponse>> CreateAsync(
        CreateAdditionalShopCommand command,
        CancellationToken cancellationToken = default);
}
