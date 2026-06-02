using System.Globalization;
using System.Text;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Routing;

public sealed class RouteClassificationService : IRouteClassificationService
{
    private static readonly IReadOnlyDictionary<string, string> ProvinceRegions = BuildProvinceRegions();

    public Result<RouteClassificationResult> Classify(
        string pickupProvince,
        string deliveryProvince)
    {
        var pickupKey = NormalizeProvinceName(pickupProvince);
        var deliveryKey = NormalizeProvinceName(deliveryProvince);

        if (!ProvinceRegions.TryGetValue(pickupKey, out var pickupRegion))
        {
            return Result<RouteClassificationResult>.Failure(
                RouteClassificationErrors.ProvinceNotSupported(pickupProvince));
        }

        if (!ProvinceRegions.TryGetValue(deliveryKey, out var deliveryRegion))
        {
            return Result<RouteClassificationResult>.Failure(
                RouteClassificationErrors.ProvinceNotSupported(deliveryProvince));
        }

        var routeType = pickupKey == deliveryKey
            ? RouteType.IntraProvince
            : pickupRegion == deliveryRegion
                ? RouteType.IntraRegion
                : RouteType.InterRegion;

        return Result<RouteClassificationResult>.Success(new RouteClassificationResult(
            routeType,
            pickupRegion,
            deliveryRegion));
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
            map[NormalizeProvinceName(province)] = region;
        }
    }

    private static string NormalizeProvinceName(string value)
    {
        var normalized = RemoveDiacritics(value)
            .Trim()
            .ToLowerInvariant();

        normalized = normalized
            .Replace("thanh pho ", string.Empty, StringComparison.Ordinal)
            .Replace("tinh ", string.Empty, StringComparison.Ordinal);

        return string.Join(
            ' ',
            normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }
}
