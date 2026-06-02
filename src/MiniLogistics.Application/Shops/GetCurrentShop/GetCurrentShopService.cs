using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetCurrentShop;

public sealed class GetCurrentShopService : IGetCurrentShopService
{
    private readonly IShopRepository _shopRepository;

    public GetCurrentShopService(IShopRepository shopRepository)
    {
        _shopRepository = shopRepository;
    }

    public async Task<Result<GetCurrentShopResponse>> GetAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var shop = await _shopRepository.GetByOwnerUserIdAsync(ownerUserId, cancellationToken);
        if (shop is null)
        {
            return Result<GetCurrentShopResponse>.Failure(ApplicationErrors.NotFound("Shop was not found for current user."));
        }

        if (!shop.IsActive)
        {
            return Result<GetCurrentShopResponse>.Failure(ApplicationErrors.Forbidden("Shop account is not active."));
        }

        return Result<GetCurrentShopResponse>.Success(new GetCurrentShopResponse(
            shop.Id,
            shop.Name,
            shop.PhoneNumber.Value,
            shop.Address.Street,
            shop.Address.Ward,
            shop.Address.Province,
            shop.Address.Country));
    }
}
