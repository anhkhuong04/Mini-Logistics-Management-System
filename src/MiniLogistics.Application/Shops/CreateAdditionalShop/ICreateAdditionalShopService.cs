using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.CreateAdditionalShop;

/// <summary>
/// Defines the application use case contract for Create Additional Shop.
/// </summary>
public interface ICreateAdditionalShopService
{
    Task<Result<CreateAdditionalShopResponse>> CreateAsync(
        CreateAdditionalShopCommand command,
        CancellationToken cancellationToken = default);
}
