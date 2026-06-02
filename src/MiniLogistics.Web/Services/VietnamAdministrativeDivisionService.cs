using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniLogistics.Web.Services;

public sealed class VietnamAdministrativeDivisionService
{
    private const string DataFileName = "vietnam-administrative-divisions.json";

    private readonly IWebHostEnvironment _environment;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<ProvinceOption>? _provinces;

    public VietnamAdministrativeDivisionService(IWebHostEnvironment environment)
    {
        _environment = environment;
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

            var filePath = Path.Combine(_environment.WebRootPath, "data", DataFileName);
            await using var stream = File.OpenRead(filePath);

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

    private sealed record ProvinceJson(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("wards")] IReadOnlyList<WardJson> Wards);

    private sealed record WardJson(
        [property: JsonPropertyName("name")] string Name);
}

public sealed record ProvinceOption(
    string Name,
    IReadOnlyList<WardOption> Wards);

public sealed record WardOption(string Name);
