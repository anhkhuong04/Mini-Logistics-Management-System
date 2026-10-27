using MiniLogistics.Application.ReferenceData;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Text;
using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Infrastructure.ReferenceData;

public sealed class VietnamAdministrativeDivisionService : IAdministrativeDivisionService, IAdministrativeDivisionCatalog
{
    private readonly string _dataFilePath;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<ProvinceOption>? _provinces;

    public VietnamAdministrativeDivisionService(string dataFilePath)
    {
        _dataFilePath = Path.GetFullPath(dataFilePath);
    }

    public async Task<IReadOnlyList<ProvinceOption>> GetProvincesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_provinces is not null)
        {
            return _provinces;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_provinces is not null)
            {
                return _provinces;
            }

            await using var stream = File.OpenRead(_dataFilePath);

            var divisions = await JsonSerializer.DeserializeAsync<List<ProvinceJson>>(
                stream,
                cancellationToken: cancellationToken);

            _provinces = (divisions ?? [])
                .Select(province => new ProvinceOption(
                    province.Name,
                    province.Wards
                        .Select(ward => new WardOption(ward.Name))
                        .OrderBy(ward => ward.Name)
                        .ToList()))
                .OrderBy(province => province.Name)
                .ToList();

            return _provinces;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task<Result<NormalizedAdministrativeDivision>> NormalizeProvinceWardAsync(
        string province,
        string ward,
        CancellationToken cancellationToken = default)
    {
        var provinces = await GetProvincesAsync(cancellationToken);
        var provinceKey = BuildLookupKey(province, "tinh", "thanhpho");
        var canonicalProvince = provinces.FirstOrDefault(item =>
            BuildLookupKey(item.Name, "tinh", "thanhpho") == provinceKey);
        if (canonicalProvince is null)
        {
            return Result<NormalizedAdministrativeDivision>.Failure(
                ApplicationErrors.ValidationFailed($"Unsupported province: {province}."));
        }

        var wardKey = BuildLookupKey(ward, "phuong", "xa", "thitran");
        var canonicalWard = canonicalProvince.Wards.FirstOrDefault(item =>
            BuildLookupKey(item.Name, "phuong", "xa", "thitran") == wardKey);
        if (canonicalWard is null)
        {
            return Result<NormalizedAdministrativeDivision>.Failure(
                ApplicationErrors.ValidationFailed(
                    $"Unsupported ward '{ward}' for province '{canonicalProvince.Name}'."));
        }

        return Result<NormalizedAdministrativeDivision>.Success(
            new NormalizedAdministrativeDivision(canonicalProvince.Name, canonicalWard.Name));
    }

    public async Task<bool> IsSupportedProvinceAsync(
        string province,
        CancellationToken cancellationToken = default)
    {
        var provinces = await GetProvincesAsync(cancellationToken);
        var provinceKey = BuildLookupKey(province, "tinh", "thanhpho");
        return provinces.Any(item =>
            BuildLookupKey(item.Name, "tinh", "thanhpho") == provinceKey);
    }

    private static string BuildLookupKey(string value, params string[] removablePrefixes)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        var key = builder.ToString();
        foreach (var prefix in removablePrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                return key[prefix.Length..];
            }
        }

        return key;
    }

    private sealed record ProvinceJson(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("wards")] IReadOnlyList<WardJson> Wards);

    private sealed record WardJson(
        [property: JsonPropertyName("name")] string Name);
}
