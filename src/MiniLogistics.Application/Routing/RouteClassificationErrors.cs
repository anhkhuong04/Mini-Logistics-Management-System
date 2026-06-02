using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Routing;

public static class RouteClassificationErrors
{
    public static Error ProvinceNotSupported(string province) =>
        new("RouteClassification.ProvinceNotSupported", $"Province is not supported for route classification: {province}");
}
