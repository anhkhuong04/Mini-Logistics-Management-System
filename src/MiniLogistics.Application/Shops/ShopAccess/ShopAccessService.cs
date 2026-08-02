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
    private readonly IShopStaffMembershipRepository? _membershipRepository;

    public ShopAccessService(
        IIdentityService identityService,
        IShopRepository shopRepository,
        IShopStaffMembershipRepository? membershipRepository = null)
    {
        _identityService = identityService;
        _shopRepository = shopRepository;
        _membershipRepository = membershipRepository;
    }

    public async Task<Result<IReadOnlyList<Shop>>> GetAccessibleShopsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await GetAccessibleShopAccessesCoreAsync(
            currentUserId,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<IReadOnlyList<Shop>>.Failure(accessResult.Error);
        }

        return Result<IReadOnlyList<Shop>>.Success(accessResult.Value.Select(access => access.Shop).ToList());
    }

    public async Task<Result<Shop>> GetShopForUserAsync(
        Guid currentUserId,
        Guid? shopId,
        bool requireActiveShop,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await GetShopAccessAsync(
            currentUserId,
            shopId,
            requireActiveShop,
            ShopPermission.ViewShipments,
            cancellationToken);
        return accessResult.IsFailure
            ? Result<Shop>.Failure(accessResult.Error)
            : Result<Shop>.Success(accessResult.Value.Shop);
    }

    public Task<Result<IReadOnlyList<ShopAccessContext>>> GetAccessibleShopAccessesAsync(
        Guid currentUserId,
        ShopPermission requiredPermission,
        CancellationToken cancellationToken = default)
    {
        return GetAccessibleShopAccessesCoreAsync(currentUserId, requiredPermission, cancellationToken);
    }

    public async Task<Result<ShopAccessContext>> GetShopAccessAsync(
        Guid currentUserId,
        Guid? shopId,
        bool requireActiveShop,
        ShopPermission requiredPermission,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await GetAccessibleShopAccessesCoreAsync(
            currentUserId,
            requiredPermission,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<ShopAccessContext>.Failure(accessResult.Error);
        }

        var accesses = accessResult.Value;
        var shops = accesses.Select(access => access.Shop).ToList();
        var shop = shopId.HasValue
            ? shops.FirstOrDefault(item => item.Id == shopId.Value)
            : SelectDefaultShop(shops);

        if (shop is null)
        {
            return shopId.HasValue
                ? Result<ShopAccessContext>.Failure(ApplicationErrors.Forbidden("Current user does not have the required permission for this shop."))
                : Result<ShopAccessContext>.Failure(ApplicationErrors.ValidationFailed("Shop id is required when current user can access multiple shops."));
        }

        if (requireActiveShop && !shop.IsActive)
        {
            return Result<ShopAccessContext>.Failure(ApplicationErrors.Forbidden("Shop account is not active."));
        }

        return Result<ShopAccessContext>.Success(accesses.First(access => access.Shop.Id == shop.Id));
    }

    private async Task<Result<IReadOnlyList<ShopAccessContext>>> GetAccessibleShopAccessesCoreAsync(
        Guid currentUserId,
        ShopPermission requiredPermission,
        CancellationToken cancellationToken)
    {
        var userCheckResult = await EnsureActiveShopUserAsync(currentUserId, cancellationToken);
        if (userCheckResult.IsFailure)
        {
            return Result<IReadOnlyList<ShopAccessContext>>.Failure(userCheckResult.Error);
        }

        var ownedShops = await _shopRepository.GetAllByOwnerUserIdAsync(currentUserId, cancellationToken);
        var accesses = ownedShops
            .Select(shop => new ShopAccessContext(shop, true, null, ShopPermission.All))
            .ToList();

        if (_membershipRepository is not null)
        {
            var memberships = await _membershipRepository.GetActiveByUserIdAsync(currentUserId, cancellationToken);
            var permittedMemberships = memberships
                .Where(membership => membership.HasPermission(requiredPermission))
                .Where(membership => accesses.All(access => access.Shop.Id != membership.ShopId))
                .ToList();
            if (permittedMemberships.Count > 0)
            {
                var memberShops = await _shopRepository.GetByIdsAsync(
                    permittedMemberships.Select(membership => membership.ShopId).Distinct().ToArray(),
                    cancellationToken);
                accesses.AddRange(memberShops.Select(shop =>
                {
                    var membership = permittedMemberships.First(item => item.ShopId == shop.Id);
                    return new ShopAccessContext(
                        shop,
                        false,
                        membership.Role,
                        membership.Permissions);
                }));
            }
        }

        accesses = accesses
            .Where(access => access.HasPermission(requiredPermission))
            .OrderByDescending(access => access.Shop.IsActive)
            .ThenBy(access => access.Shop.Name)
            .ToList();
        return accesses.Count == 0
            ? Result<IReadOnlyList<ShopAccessContext>>.Failure(
                ApplicationErrors.Forbidden("Current user does not have the required permission for any shop."))
            : Result<IReadOnlyList<ShopAccessContext>>.Success(accesses);
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
