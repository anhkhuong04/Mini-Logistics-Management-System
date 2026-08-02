using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Common;

public interface IAdministrativeDivisionService
{
    Task<Result<NormalizedAdministrativeDivision>> NormalizeProvinceWardAsync(
        string province,
        string ward,
        CancellationToken cancellationToken = default);

    Task<bool> IsSupportedProvinceAsync(
        string province,
        CancellationToken cancellationToken = default);
}

public sealed record NormalizedAdministrativeDivision(
    string Province,
    string Ward);

internal sealed class PassThroughAdministrativeDivisionService : IAdministrativeDivisionService
{
    public static readonly PassThroughAdministrativeDivisionService Instance = new();

    private PassThroughAdministrativeDivisionService()
    {
    }

    public Task<Result<NormalizedAdministrativeDivision>> NormalizeProvinceWardAsync(
        string province,
        string ward,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<NormalizedAdministrativeDivision>.Success(
            new NormalizedAdministrativeDivision(province.Trim(), ward.Trim())));
    }

    public Task<bool> IsSupportedProvinceAsync(
        string province,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(province));
    }
}
