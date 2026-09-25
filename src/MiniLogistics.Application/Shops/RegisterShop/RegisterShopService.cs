using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MiniLogistics.Application.Shops.RegisterShop;

public sealed class RegisterShopService : IRegisterShopService
{
    public const string ShopRole = "Shop";

    private readonly IValidator<RegisterShopCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShopRepository _shopRepository;
    private readonly IApplicationDbTransactionManager _transactionManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RegisterShopService> _logger;

    public RegisterShopService(
        IValidator<RegisterShopCommand> validator,
        IIdentityService identityService,
        IShopRepository shopRepository,
        TimeProvider timeProvider,
        IApplicationDbTransactionManager transactionManager,
        ILogger<RegisterShopService> logger)
    {
        _validator = validator;
        _identityService = identityService;
        _shopRepository = shopRepository;
        _timeProvider = timeProvider;
        _transactionManager = transactionManager;
        _logger = logger;
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

        try
        {
            await using var transaction = await _transactionManager.BeginTransactionAsync(cancellationToken);

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
                    command.Country),
                _timeProvider.GetUtcNow());

            await _shopRepository.AddAsync(shop, cancellationToken);
            await _shopRepository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RegisterShopResponse>.Success(new RegisterShopResponse(
                userResult.Value,
                shop.Id,
                command.Email,
                shop.Name));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Shop registration failed; the transaction was not committed.");
            return Result<RegisterShopResponse>.Failure(ApplicationErrors.RegistrationFailed(
                "Shop registration could not be completed. Please try again."));
        }
    }
}
