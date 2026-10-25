using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
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
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;
    private readonly IAdministrativeDivisionService _administrativeDivisionService;

    public CreateAdditionalShopService(
        IValidator<CreateAdditionalShopCommand> validator,
        IShopAccessService shopAccessService,
        IShopRepository shopRepository,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
        : this(validator, shopAccessService, shopRepository, PassThroughAdministrativeDivisionService.Instance, timeProvider, adminAuditService)
    {
    }

    public CreateAdditionalShopService(
        IValidator<CreateAdditionalShopCommand> validator,
        IShopAccessService shopAccessService,
        IShopRepository shopRepository,
        IAdministrativeDivisionService administrativeDivisionService,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _shopAccessService = shopAccessService;
        _shopRepository = shopRepository;
        _timeProvider = timeProvider;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _administrativeDivisionService = administrativeDivisionService;
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

        var accessResult = await _shopAccessService.GetAccessibleShopAccessesAsync(
            command.CurrentUserId,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<CreateAdditionalShopResponse>.Failure(accessResult.Error);
        }

        if (!accessResult.Value.Any(access => access.IsOwner))
        {
            return Result<CreateAdditionalShopResponse>.Failure(
                ApplicationErrors.Forbidden("Only a shop owner can create an additional shop."));
        }

        var division = await _administrativeDivisionService.NormalizeProvinceWardAsync(command.Province, command.Ward, cancellationToken);
        if (division.IsFailure) return Result<CreateAdditionalShopResponse>.Failure(division.Error);
        try
        {
            var shop = new Shop(
                command.CurrentUserId,
                command.Name,
                new PhoneNumber(command.PhoneNumber),
                new Address(
                    command.AddressLine,
                    division.Value.Ward,
                    division.Value.Province,
                    command.Country),
                _timeProvider.GetUtcNow());

            await _shopRepository.AddAsync(shop, cancellationToken);
            await _adminAuditService.RecordAsync(
                new AdminAuditEntry(
                    command.CurrentUserId,
                    AdminAuditActions.ShopAdditionalShopCreated,
                    AdminAuditTargetTypes.Shop,
                    shop.Id,
                    NewValue: new
                    {
                        shop.Name,
                        PhoneNumber = shop.PhoneNumber.Value,
                        Address = shop.Address.FullAddress,
                        shop.IsActive
                    }),
                cancellationToken);
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
