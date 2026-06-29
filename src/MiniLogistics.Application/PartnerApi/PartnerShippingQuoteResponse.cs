using System.Text.Json.Serialization;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.PartnerApi;

public sealed record PartnerShippingQuoteResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<RouteType>))]
    RouteType RouteType,
    decimal ActualWeightKg,
    decimal VolumetricWeightKg,
    decimal ChargeableWeightKg,
    decimal BaseFeeAmount,
    decimal ExtraWeightFeeAmount,
    decimal InsuranceFeeAmount,
    decimal ReturnFeeAmount,
    decimal TotalFeeAmount,
    string Currency);
