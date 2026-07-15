using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shops.ShopAccess;

public sealed class ShopAccessService : IShopAccessService
{
    private readonly IIdentityService _identityService;
    private readonly IShopRepository _shopRepository;

    public ShopAccessService(
        IIdentityService identityService,
        IShopRepository shopRepository)
    {
        _identityService = identityService;
        _shopRepository = shopRepository;
    }

    public async Task<Result<IReadOnlyList<Shop>>> GetAccessibleShopsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var userCheckResult = await EnsureActiveShopUserAsync(currentUserId, cancellationToken);
        if (userCheckResult.IsFailure)
        {
            return Result<IReadOnlyList<Shop>>.Failure(userCheckResult.Error);
        }

        var shops = await _shopRepository.GetAllByOwnerUserIdAsync(currentUserId, cancellationToken);
        if (shops.Count == 0)
        {
            return Result<IReadOnlyList<Shop>>.Failure(
                ApplicationErrors.NotFound("Shop was not found for current user."));
        }

        return Result<IReadOnlyList<Shop>>.Success(shops);
    }

    public async Task<Result<Shop>> GetShopForUserAsync(
        Guid currentUserId,
        Guid? shopId,
        bool requireActiveShop,
        CancellationToken cancellationToken = default)
    {
        var shopsResult = await GetAccessibleShopsAsync(currentUserId, cancellationToken);
        if (shopsResult.IsFailure)
        {
            return Result<Shop>.Failure(shopsResult.Error);
        }

        var shops = shopsResult.Value;
        var shop = shopId.HasValue
            ? shops.FirstOrDefault(item => item.Id == shopId.Value)
            : SelectDefaultShop(shops);

        if (shop is null)
        {
            return shopId.HasValue
                ? Result<Shop>.Failure(ApplicationErrors.Forbidden("Current user cannot access this shop."))
                : Result<Shop>.Failure(ApplicationErrors.ValidationFailed("Shop id is required when current user owns multiple shops."));
        }

        if (requireActiveShop && !shop.IsActive)
        {
            return Result<Shop>.Failure(ApplicationErrors.Forbidden("Shop account is not active."));
        }

        return Result<Shop>.Success(shop);
    }

    private async Task<Result> EnsureActiveShopUserAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result.Failure(ApplicationErrors.ValidationFailed("Current user id is required."));
        }

        var shopCheck = await _identityService.CheckUserRoleAsync(
            currentUserId,
            nameof(UserRole.Shop),
            cancellationToken);

        if (!shopCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Current user was not found."));
        }

        if (!shopCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Current user is not active."));
        }

        return shopCheck.IsInRole
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden("Only Shop users can access shop data."));
    }

    private static Shop? SelectDefaultShop(IReadOnlyList<Shop> shops)
    {
        if (shops.Count == 1)
        {
            return shops[0];
        }

        return null;
    }
}
