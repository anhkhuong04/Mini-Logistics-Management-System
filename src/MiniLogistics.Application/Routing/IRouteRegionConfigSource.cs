namespace MiniLogistics.Application.Routing;

/// <summary>
/// Defines read access to Route Region Config Source data.
/// </summary>
public interface IRouteRegionConfigSource
{
    IReadOnlyDictionary<string, string> GetProvinceRegions();
}

public sealed class DefaultRouteRegionConfigSource : IRouteRegionConfigSource
{
    public static readonly DefaultRouteRegionConfigSource Instance = new();

    private static readonly IReadOnlyDictionary<string, string> ProvinceRegions = BuildProvinceRegions();

    private DefaultRouteRegionConfigSource()
    {
    }

    public IReadOnlyDictionary<string, string> GetProvinceRegions()
    {
        return ProvinceRegions;
    }

    private static IReadOnlyDictionary<string, string> BuildProvinceRegions()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddRegion(map, "Northern Midlands and Mountains",
            "Cao Bang",
            "Tuyen Quang",
            "Dien Bien",
            "Lai Chau",
            "Son La",
            "Lao Cai",
            "Thai Nguyen",
            "Lang Son",
            "Phu Tho");

        AddRegion(map, "Red River Delta",
            "Ha Noi",
            "Hai Phong",
            "Quang Ninh",
            "Bac Ninh",
            "Hung Yen",
            "Ninh Binh");

        AddRegion(map, "North Central and Central Coast",
            "Thanh Hoa",
            "Nghe An",
            "Ha Tinh",
            "Quang Tri",
            "Hue",
            "Da Nang",
            "Quang Ngai",
            "Khanh Hoa");

        AddRegion(map, "Central Highlands",
            "Gia Lai",
            "Dak Lak",
            "Lam Dong");

        AddRegion(map, "Southeast",
            "Dong Nai",
            "Ho Chi Minh",
            "Tay Ninh");

        AddRegion(map, "Mekong Delta",
            "Dong Thap",
            "Vinh Long",
            "An Giang",
            "Can Tho",
            "Ca Mau");

        return map;
    }

    private static void AddRegion(
        IDictionary<string, string> map,
        string region,
        params string[] provinces)
    {
        foreach (var province in provinces)
        {
            map[province] = region;
        }
    }
}
