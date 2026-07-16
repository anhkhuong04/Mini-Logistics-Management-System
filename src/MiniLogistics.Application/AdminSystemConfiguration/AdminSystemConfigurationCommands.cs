using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.AdminSystemConfiguration;

public sealed record UpsertRouteRegionConfigCommand(
    Guid RequestedByUserId,
    string Province,
    string Region,
    string? Reason = null);

public sealed record CreateFeeRuleVersionCommand(
    Guid RequestedByUserId,
    RouteType RouteType,
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
    string? Reason = null);
