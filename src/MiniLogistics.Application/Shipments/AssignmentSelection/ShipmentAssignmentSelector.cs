using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.AssignmentSelection;

public sealed class ShipmentAssignmentSelector : IShipmentAssignmentSelector
{
    private readonly IIdentityService _identityService;
    private readonly IHubRepository _hubRepository;
    private readonly IShipperWorkingAreaRepository _workingAreaRepository;
    private readonly IShipmentReadRepository _shipmentRepository;

    public ShipmentAssignmentSelector(
        IIdentityService identityService,
        IHubRepository hubRepository,
        IShipperWorkingAreaRepository workingAreaRepository,
        IShipmentReadRepository shipmentRepository)
    {
        _identityService = identityService;
        _hubRepository = hubRepository;
        _workingAreaRepository = workingAreaRepository;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<ShipmentAssignmentSelectionResult> SelectAsync(
        Shipment shipment,
        CancellationToken cancellationToken = default)
    {
        var activeShippers = await _identityService.GetActiveShippersAsync(cancellationToken);
        if (activeShippers.Count == 0)
        {
            return ShipmentAssignmentSelectionResult.NoEligibleShipper("No active shipper is available.");
        }

        var availableShippers = activeShippers
            .Where(shipper => shipper.IsAvailableForAssignment)
            .ToList();
        if (availableShippers.Count == 0)
        {
            return ShipmentAssignmentSelectionResult.NoEligibleShipper("No active shipper is available for auto assignment.");
        }

        var pickupProvinceKey = LocationNameNormalizer.NormalizeProvince(shipment.PickupAddress.Province);
        var pickupWardKey = LocationNameNormalizer.NormalizeAreaValue(shipment.PickupAddress.Ward);
        var hubs = await _hubRepository.GetAllAsync(activeOnly: true, cancellationToken);
        var pickupHub = SelectPickupHub(hubs, pickupProvinceKey);

        var availableShipperIds = availableShippers
            .Select(shipper => shipper.UserId)
            .ToHashSet();
        var workingAreas = await _workingAreaRepository.GetActiveByShipperIdsAsync(
            availableShipperIds.ToList(),
            cancellationToken);
        var candidateAreas = workingAreas
            .Where(area => availableShipperIds.Contains(area.ShipperId))
            .Select(area => ToCandidateArea(area, pickupHub, pickupProvinceKey, pickupWardKey))
            .Where(area => area is not null)
            .Select(area => area!)
            .ToList();

        if (candidateAreas.Count == 0)
        {
            var reason = pickupHub is null
                ? $"No shipper working area matches pickup province {shipment.PickupAddress.Province}."
                : $"No shipper working area matches pickup hub {pickupHub.Code}.";
            return ShipmentAssignmentSelectionResult.NoEligibleShipper(reason);
        }

        var candidateShipperIds = candidateAreas
            .Select(area => area.ShipperId)
            .Distinct()
            .ToList();
        var activeLoadByShipperId = await _shipmentRepository.GetActiveAssignmentCountsByShipperIdsAsync(
            candidateShipperIds,
            cancellationToken);
        var shipperById = availableShippers.ToDictionary(shipper => shipper.UserId);

        var candidatesWithinCapacity = candidateAreas
            .Select(area =>
            {
                var shipper = shipperById[area.ShipperId];
                activeLoadByShipperId.TryGetValue(area.ShipperId, out var activeLoad);

                return new AssignmentCandidate(
                    area.ShipperId,
                    shipper.FullName,
                    area.WorkingAreaId,
                    area.HubId,
                    area.HubCode,
                    area.MatchScore,
                    activeLoad,
                    shipper.MaxActiveShipments);
            })
            .Where(candidate => candidate.ActiveShipmentCount < candidate.MaxActiveShipments)
            .ToList();

        if (candidatesWithinCapacity.Count == 0)
        {
            var reason = pickupHub is null
                ? $"All matching shippers for pickup province {shipment.PickupAddress.Province} are at capacity."
                : $"All matching shippers for pickup hub {pickupHub.Code} are at capacity.";
            return ShipmentAssignmentSelectionResult.NoEligibleShipper(reason);
        }

        var selected = candidatesWithinCapacity
            .OrderByDescending(candidate => candidate.MatchScore)
            .ThenBy(candidate => candidate.ActiveShipmentCount)
            .ThenBy(candidate => candidate.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.ShipperId)
            .First();

        var selectedReason = selected.HubCode is null
            ? $"Matched pickup province {shipment.PickupAddress.Province}; active load {selected.ActiveShipmentCount}."
            : $"Matched pickup hub {selected.HubCode}; active load {selected.ActiveShipmentCount}.";

        return ShipmentAssignmentSelectionResult.Selected(
            selected.ShipperId,
            selected.WorkingAreaId,
            selected.HubId,
            selected.HubCode,
            selected.ActiveShipmentCount,
            selectedReason);
    }

    private static Hub? SelectPickupHub(
        IEnumerable<Hub> hubs,
        string pickupProvinceKey)
    {
        return hubs
            .Where(hub => LocationNameNormalizer.NormalizeProvince(hub.Province) == pickupProvinceKey)
            .OrderBy(hub => hub.IsRegionalSortingHub)
            .ThenBy(hub => hub.Name)
            .FirstOrDefault();
    }

    private static CandidateArea? ToCandidateArea(
        ShipperWorkingArea area,
        Hub? pickupHub,
        string pickupProvinceKey,
        string pickupWardKey)
    {
        var areaProvinceKey = LocationNameNormalizer.NormalizeProvince(area.Province);
        var areaWardKey = LocationNameNormalizer.NormalizeAreaValue(area.Ward);
        var areaZoneKey = LocationNameNormalizer.NormalizeAreaValue(area.ZoneCode);

        if (!string.IsNullOrWhiteSpace(areaZoneKey))
        {
            return null;
        }

        var hubMatches = pickupHub is not null && area.HubId == pickupHub.Id;
        var provinceMatches = areaProvinceKey == pickupProvinceKey;
        if (!hubMatches && !provinceMatches)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(areaWardKey) && areaWardKey != pickupWardKey)
        {
            return null;
        }

        var wardScore = string.IsNullOrWhiteSpace(areaWardKey) ? 0 : 10;
        var hubScore = hubMatches ? 20 : 0;
        var provinceScore = provinceMatches ? 5 : 0;

        return new CandidateArea(
            area.ShipperId,
            area.Id,
            hubMatches ? pickupHub?.Id : null,
            hubMatches ? pickupHub?.Code : null,
            hubScore + wardScore + provinceScore);
    }

    private sealed record CandidateArea(
        Guid ShipperId,
        Guid WorkingAreaId,
        Guid? HubId,
        string? HubCode,
        int MatchScore);

    private sealed record AssignmentCandidate(
        Guid ShipperId,
        string FullName,
        Guid WorkingAreaId,
        Guid? HubId,
        string? HubCode,
        int MatchScore,
        int ActiveShipmentCount,
        int MaxActiveShipments);
}
