using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetAdminShops;

public sealed class GetAdminShopsService : IGetAdminShopsService
{
    private readonly IIdentityService _identityService;
    private readonly IShopRepository _shopRepository;

    public GetAdminShopsService(
        IIdentityService identityService,
        IShopRepository shopRepository)
    {
        _identityService = identityService;
        _shopRepository = shopRepository;
    }

    public async Task<Result<IReadOnlyList<GetAdminShopResponse>>> GetAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            currentUserId,
            cancellationToken);

        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyList<GetAdminShopResponse>>.Failure(authorizationResult.Error);
        }

        var shops = await _shopRepository.GetAllAsync(cancellationToken);
        var ownerIds = shops
            .Select(shop => shop.OwnerUserId)
            .Distinct()
            .ToList();
        var owners = await _identityService.GetUsersByIdsAsync(ownerIds, cancellationToken);
        var ownerById = owners.ToDictionary(owner => owner.UserId);

        var response = shops
            .Select(shop =>
            {
                ownerById.TryGetValue(shop.OwnerUserId, out var owner);

                return new GetAdminShopResponse(
                    shop.Id,
                    shop.OwnerUserId,
                    owner?.FullName ?? "Unknown owner",
                    owner?.Email ?? string.Empty,
                    owner?.PhoneNumber,
                    shop.Name,
                    shop.PhoneNumber.Value,
                    shop.Address.Street,
                    shop.Address.Ward,
                    shop.Address.Province,
                    shop.Address.Country,
                    shop.IsActive,
                    shop.CreatedAtUtc,
                    shop.UpdatedAtUtc);
            })
            .ToList();

        return Result<IReadOnlyList<GetAdminShopResponse>>.Success(response);
    }
}
