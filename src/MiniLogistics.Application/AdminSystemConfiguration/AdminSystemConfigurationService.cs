using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Routing;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.AdminSystemConfiguration;

public sealed class AdminSystemConfigurationService : IAdminSystemConfigurationService
{
    private readonly IIdentityService _identityService;
    private readonly IRouteRegionConfigRepository _routeRegionConfigRepository;
    private readonly IFeeConfigurationRepository _feeConfigurationRepository;
    private readonly IValidator<UpsertRouteRegionConfigCommand> _routeValidator;
    private readonly IValidator<CreateFeeRuleVersionCommand> _feeValidator;
    private readonly IAdminAuditService _adminAuditService;

    public AdminSystemConfigurationService(
        IIdentityService identityService,
        IRouteRegionConfigRepository routeRegionConfigRepository,
        IFeeConfigurationRepository feeConfigurationRepository,
        IValidator<UpsertRouteRegionConfigCommand> routeValidator,
        IValidator<CreateFeeRuleVersionCommand> feeValidator,
        IAdminAuditService? adminAuditService = null)
    {
        _identityService = identityService;
        _routeRegionConfigRepository = routeRegionConfigRepository;
        _feeConfigurationRepository = feeConfigurationRepository;
        _routeValidator = routeValidator;
        _feeValidator = feeValidator;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result<AdminSystemConfigurationResponse>> GetAsync(
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            requestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<AdminSystemConfigurationResponse>.Failure(authorizationResult.Error);
        }

        var routeRegions = await _routeRegionConfigRepository.GetAllAsync(
            activeOnly: false,
            cancellationToken);
        var feeRules = await _feeConfigurationRepository.GetAllAsync(cancellationToken);

        return Result<AdminSystemConfigurationResponse>.Success(
            new AdminSystemConfigurationResponse(
                routeRegions
                    .OrderBy(config => config.Province)
                    .ThenByDescending(config => config.Version)
                    .Select(ToResponse)
                    .ToList(),
                feeRules
                    .OrderBy(rule => rule.RouteType)
                    .ThenByDescending(rule => rule.Version)
                    .Select(ToResponse)
                    .ToList()));
    }

    public async Task<Result<RouteRegionConfigResponse>> UpsertRouteRegionAsync(
        UpsertRouteRegionConfigCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _routeValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<RouteRegionConfigResponse>.Failure(ToValidationError(validationResult));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            command.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RouteRegionConfigResponse>.Failure(authorizationResult.Error);
        }

        var oldConfigs = await _routeRegionConfigRepository.GetActiveByProvinceAsync(
            command.Province,
            cancellationToken);
        foreach (var oldConfig in oldConfigs)
        {
            oldConfig.Deactivate();
        }

        var version = await _routeRegionConfigRepository.GetLatestVersionAsync(
            command.Province,
            cancellationToken) + 1;
        var config = new RouteRegionConfig(command.Province, command.Region, version);
        await _routeRegionConfigRepository.AddAsync(config, cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.RequestedByUserId,
                AdminAuditActions.RouteRegionConfigChanged,
                AdminAuditTargetTypes.RouteRegionConfig,
                config.Id,
                OldValue: oldConfigs.Select(ToAuditValue).ToList(),
                NewValue: ToAuditValue(config),
                Reason: command.Reason,
                ActorRole: nameof(UserRole.Admin)),
            cancellationToken);
        await _routeRegionConfigRepository.SaveChangesAsync(cancellationToken);

        return Result<RouteRegionConfigResponse>.Success(ToResponse(config));
    }

    public async Task<Result<FeeRuleConfigResponse>> CreateFeeRuleVersionAsync(
        CreateFeeRuleVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _feeValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<FeeRuleConfigResponse>.Failure(ToValidationError(validationResult));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            command.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<FeeRuleConfigResponse>.Failure(authorizationResult.Error);
        }

        var oldRules = await _feeConfigurationRepository.GetActiveRulesForUpdateAsync(
            command.RouteType,
            cancellationToken);
        foreach (var oldRule in oldRules)
        {
            oldRule.Deactivate();
        }

        var version = await _feeConfigurationRepository.GetLatestVersionAsync(
            command.RouteType,
            cancellationToken) + 1;
        var newRule = new FeeRule(
            command.RouteType,
            command.BaseWeightKg,
            new Money(command.BaseFeeAmount),
            command.ExtraWeightStepKg,
            new Money(command.ExtraStepFeeAmount),
            command.MinimumWeightKg,
            command.MaximumWeightKg,
            version,
            command.InsuranceFreeThreshold,
            command.InsuranceMaximumValue,
            command.InsuranceRate,
            command.ReturnFeeRate);

        await _feeConfigurationRepository.AddAsync(newRule, cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.RequestedByUserId,
                AdminAuditActions.FeeRuleVersionCreated,
                AdminAuditTargetTypes.FeeRule,
                newRule.Id,
                OldValue: oldRules.Select(ToAuditValue).ToList(),
                NewValue: ToAuditValue(newRule),
                Reason: command.Reason,
                ActorRole: nameof(UserRole.Admin)),
            cancellationToken);
        await _feeConfigurationRepository.SaveChangesAsync(cancellationToken);

        return Result<FeeRuleConfigResponse>.Success(ToResponse(newRule));
    }

    private static Error ToValidationError(FluentValidation.Results.ValidationResult validationResult)
    {
        var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
        return ApplicationErrors.ValidationFailed(description);
    }

    private static RouteRegionConfigResponse ToResponse(RouteRegionConfig config)
    {
        return new RouteRegionConfigResponse(
            config.Id,
            config.Province,
            config.Region,
            config.Version,
            config.IsActive,
            config.CreatedAtUtc,
            config.UpdatedAtUtc);
    }

    private static FeeRuleConfigResponse ToResponse(FeeRule rule)
    {
        return new FeeRuleConfigResponse(
            rule.Id,
            rule.RouteType,
            rule.Version,
            rule.BaseWeightKg,
            rule.BaseFee.Amount,
            rule.ExtraWeightStepKg,
            rule.ExtraStepFee.Amount,
            rule.MinimumWeightKg,
            rule.MaximumWeightKg,
            rule.InsuranceFreeThreshold,
            rule.InsuranceMaximumValue,
            rule.InsuranceRate,
            rule.ReturnFeeRate,
            rule.IsActive,
            rule.CreatedAtUtc,
            rule.UpdatedAtUtc);
    }

    private static object ToAuditValue(RouteRegionConfig config)
    {
        return new
        {
            config.Province,
            config.Region,
            config.Version,
            config.IsActive
        };
    }

    private static object ToAuditValue(FeeRule rule)
    {
        return new
        {
            rule.RouteType,
            rule.Version,
            rule.BaseWeightKg,
            BaseFeeAmount = rule.BaseFee.Amount,
            rule.ExtraWeightStepKg,
            ExtraStepFeeAmount = rule.ExtraStepFee.Amount,
            rule.MinimumWeightKg,
            rule.MaximumWeightKg,
            rule.InsuranceFreeThreshold,
            rule.InsuranceMaximumValue,
            rule.InsuranceRate,
            rule.ReturnFeeRate,
            rule.IsActive
        };
    }
}
