using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shops.RegisterShop;

public sealed class RegisterShopService : IRegisterShopService
{
    public const string ShopRole = "Shop";

    private readonly IValidator<RegisterShopCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShopRepository _shopRepository;

    public RegisterShopService(
        IValidator<RegisterShopCommand> validator,
        IIdentityService identityService,
        IShopRepository shopRepository)
    {
        _validator = validator;
        _identityService = identityService;
        _shopRepository = shopRepository;
    }

    public async Task<Result<RegisterShopResponse>> RegisterAsync(
        RegisterShopCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<RegisterShopResponse>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var userResult = await _identityService.CreateUserAsync(
            command.FullName,
            command.Email,
            command.PhoneNumber,
            command.Password,
            cancellationToken);

        if (userResult.IsFailure)
        {
            return Result<RegisterShopResponse>.Failure(userResult.Error);
        }

        var roleResult = await _identityService.AddToRoleAsync(userResult.Value, ShopRole, cancellationToken);
        if (roleResult.IsFailure)
        {
            return Result<RegisterShopResponse>.Failure(roleResult.Error);
        }

        var shopExists = await _shopRepository.ExistsByOwnerUserIdAsync(userResult.Value, cancellationToken);
        if (shopExists)
        {
            return Result<RegisterShopResponse>.Failure(ApplicationErrors.Conflict("Shop already exists for this user."));
        }

        var shop = new Shop(
            userResult.Value,
            command.ShopName,
            new PhoneNumber(command.PhoneNumber),
            new Address(
                command.AddressLine,
                command.Ward,
                command.Province,
                command.Country));

        await _shopRepository.AddAsync(shop, cancellationToken);
        await _shopRepository.SaveChangesAsync(cancellationToken);

        return Result<RegisterShopResponse>.Success(new RegisterShopResponse(
            userResult.Value,
            shop.Id,
            command.Email,
            shop.Name));
    }
}
