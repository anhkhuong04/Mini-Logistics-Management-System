using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Routing;

public interface IRouteClassificationService
{
    Result<RouteClassificationResult> Classify(
        string pickupProvince,
        string deliveryProvince);
}

public sealed record RouteClassificationResult(
    RouteType RouteType,
    string PickupRegion,
    string DeliveryRegion);
