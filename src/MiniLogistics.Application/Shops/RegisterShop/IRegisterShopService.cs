using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.RegisterShop;

/// <summary>
/// Defines the application use case contract for Register Shop.
/// </summary>
public interface IRegisterShopService
{
    Task<Result<RegisterShopResponse>> RegisterAsync(
        RegisterShopCommand command,
        CancellationToken cancellationToken = default);
}
