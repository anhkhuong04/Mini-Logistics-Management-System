using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.CashOnDelivery.MarkCodSettled;

public sealed class MarkCodSettledService : IMarkCodSettledService
{
    private readonly IValidator<MarkCodSettledCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly ICodTransactionRepository _codTransactionRepository;

    public MarkCodSettledService(
        IValidator<MarkCodSettledCommand> validator,
        IIdentityService identityService,
        ICodTransactionRepository codTransactionRepository)
    {
        _validator = validator;
        _identityService = identityService;
        _codTransactionRepository = codTransactionRepository;
    }

    public async Task<Result> MarkSettledAsync(
        MarkCodSettledCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var authorizationResult = await ValidateSettlementPermissionAsync(
            command.SettledByUserId,
            cancellationToken);

        if (authorizationResult.IsFailure)
        {
            return authorizationResult;
        }

        var codTransaction = await _codTransactionRepository.GetTrackedByShipmentIdAsync(
            command.ShipmentId,
            cancellationToken);

        if (codTransaction is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("COD transaction was not found."));
        }

        var settleResult = codTransaction.MarkSettled(command.SettledByUserId);
        if (settleResult.IsFailure)
        {
            return settleResult;
        }

        await _codTransactionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateSettlementPermissionAsync(
        Guid settledByUserId,
        CancellationToken cancellationToken)
    {
        var adminCheck = await _identityService.CheckUserRoleAsync(
            settledByUserId,
            nameof(UserRole.Admin),
            cancellationToken);

        if (!adminCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("COD settlement user was not found."));
        }

        if (!adminCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("COD settlement user is not active."));
        }

        return adminCheck.IsInRole
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden("Only Admin can settle COD."));
    }
}
