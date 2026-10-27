namespace MiniLogistics.Application.ReferenceData;

/// <summary>Provides the authoritative province and ward names for browser reference data.</summary>
public interface IAdministrativeDivisionCatalog
{
    Task<IReadOnlyList<ProvinceOption>> GetProvincesAsync(CancellationToken cancellationToken = default);
}

public sealed record ProvinceOption(string Name, IReadOnlyList<WardOption> Wards);

public sealed record WardOption(string Name);
