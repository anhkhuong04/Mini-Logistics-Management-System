using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shops.CreateAdditionalShop;

public sealed class CreateAdditionalShopService : ICreateAdditionalShopService
{
    private readonly IValidator<CreateAdditionalShopCommand> _validator;
    private readonly IShopAccessService _shopAccessService;
    private readonly IShopRepository _shopRepository;

    public CreateAdditionalShopService(
        IValidator<CreateAdditionalShopCommand> validator,
        IShopAccessService shopAccessService,
        IShopRepository shopRepository)
    {
        _validator = validator;
        _shopAccessService = shopAccessService;
        _shopRepository = shopRepository;
    }

    public async Task<Result<CreateAdditionalShopResponse>> CreateAsync(
        CreateAdditionalShopCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<CreateAdditionalShopResponse>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var accessResult = await _shopAccessService.GetAccessibleShopsAsync(
            command.CurrentUserId,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<CreateAdditionalShopResponse>.Failure(accessResult.Error);
        }

        try
        {
            var shop = new Shop(
                command.CurrentUserId,
                command.Name,
                new PhoneNumber(command.PhoneNumber),
                new Address(
                    command.AddressLine,
                    command.Ward,
                    command.Province,
                    command.Country));

            await _shopRepository.AddAsync(shop, cancellationToken);
            await _shopRepository.SaveChangesAsync(cancellationToken);

            return Result<CreateAdditionalShopResponse>.Success(new CreateAdditionalShopResponse(
                shop.Id,
                shop.Name,
                shop.IsActive));
        }
        catch (DomainException exception)
        {
            return Result<CreateAdditionalShopResponse>.Failure(
                ApplicationErrors.ValidationFailed(exception.Message));
        }
    }
}
