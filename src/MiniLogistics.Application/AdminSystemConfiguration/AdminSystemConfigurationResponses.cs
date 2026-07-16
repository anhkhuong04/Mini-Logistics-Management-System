using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.AdminSystemConfiguration;

public sealed record AdminSystemConfigurationResponse(
    IReadOnlyList<RouteRegionConfigResponse> RouteRegions,
    IReadOnlyList<FeeRuleConfigResponse> FeeRules);

public sealed record RouteRegionConfigResponse(
    Guid ConfigId,
    string Province,
    string Region,
    int Version,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record FeeRuleConfigResponse(
    Guid FeeRuleId,
    RouteType RouteType,
    int Version,
    decimal BaseWeightKg,
    decimal BaseFeeAmount,
    decimal ExtraWeightStepKg,
    decimal ExtraStepFeeAmount,
    decimal? MinimumWeightKg,
    decimal? MaximumWeightKg,
    decimal InsuranceFreeThreshold,
    decimal InsuranceMaximumValue,
    decimal InsuranceRate,
    decimal ReturnFeeRate,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
