using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Application.Shippers;

internal static class ShipperWorkingAreaResponseMapper
{
    public static IReadOnlyList<ShipperWorkingAreaResponse> ToResponses(
        IEnumerable<ShipperWorkingArea> workingAreas,
        IReadOnlyDictionary<Guid, Hub> hubById)
    {
        return workingAreas
            .Select(area => ToResponse(area, hubById))
            .OrderBy(area => area.Province)
            .ThenBy(area => area.Ward)
            .ThenBy(area => area.ZoneCode)
            .ToList();
    }

    private static ShipperWorkingAreaResponse ToResponse(
        ShipperWorkingArea area,
        IReadOnlyDictionary<Guid, Hub> hubById)
    {
        var hub = hubById.GetValueOrDefault(area.HubId);

        return new ShipperWorkingAreaResponse(
            area.Id,
            area.ShipperId,
            area.HubId,
            hub?.Code ?? string.Empty,
            hub?.Name ?? "Unknown hub",
            area.Province,
            area.Ward,
            area.ZoneCode,
            area.IsActive);
    }
}
