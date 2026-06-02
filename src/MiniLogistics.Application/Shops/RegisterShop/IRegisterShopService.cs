using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.RegisterShop;

public interface IRegisterShopService
{
    Task<Result<RegisterShopResponse>> RegisterAsync(
        RegisterShopCommand command,
        CancellationToken cancellationToken = default);
}
