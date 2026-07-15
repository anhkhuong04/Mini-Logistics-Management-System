using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shops.UpdateShopProfile;

public sealed class UpdateShopProfileService : IUpdateShopProfileService
{
    private readonly IValidator<UpdateShopProfileCommand> _validator;
    private readonly IShopAccessService _shopAccessService;
    private readonly IShopRepository _shopRepository;

    public UpdateShopProfileService(
        IValidator<UpdateShopProfileCommand> validator,
        IShopAccessService shopAccessService,
        IShopRepository shopRepository)
    {
        _validator = validator;
        _shopAccessService = shopAccessService;
        _shopRepository = shopRepository;
    }

    public async Task<Result<UpdateShopProfileResponse>> UpdateAsync(
        UpdateShopProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<UpdateShopProfileResponse>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var shopResult = await _shopAccessService.GetShopForUserAsync(
            command.CurrentUserId,
            command.ShopId,
            requireActiveShop: true,
            cancellationToken);

        if (shopResult.IsFailure)
        {
            return Result<UpdateShopProfileResponse>.Failure(shopResult.Error);
        }

        var shop = shopResult.Value;
        try
        {
            shop.Rename(command.Name);
            shop.UpdateContact(
                new PhoneNumber(command.PhoneNumber),
                new Address(
                    command.AddressLine,
                    command.Ward,
                    command.Province,
                    command.Country));
        }
        catch (DomainException exception)
        {
            return Result<UpdateShopProfileResponse>.Failure(
                ApplicationErrors.ValidationFailed(exception.Message));
        }

        await _shopRepository.SaveChangesAsync(cancellationToken);

        return Result<UpdateShopProfileResponse>.Success(new UpdateShopProfileResponse(
            shop.Id,
            shop.Name,
            shop.PhoneNumber.Value,
            shop.Address.Street,
            shop.Address.Ward,
            shop.Address.Province,
            shop.Address.Country,
            shop.IsActive,
            shop.UpdatedAtUtc));
    }
}
