using System.Globalization;
using System.Text;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Routing;

public sealed class RouteClassificationService : IRouteClassificationService
{
    private readonly IRouteRegionConfigSource _configSource;

    public RouteClassificationService(IRouteRegionConfigSource? configSource = null)
    {
        _configSource = configSource ?? DefaultRouteRegionConfigSource.Instance;
    }

    public Result<RouteClassificationResult> Classify(
        string pickupProvince,
        string deliveryProvince)
    {
        var provinceRegions = _configSource
            .GetProvinceRegions()
            .ToDictionary(
                item => NormalizeProvinceName(item.Key),
                item => item.Value,
                StringComparer.OrdinalIgnoreCase);
        var pickupKey = NormalizeProvinceName(pickupProvince);
        var deliveryKey = NormalizeProvinceName(deliveryProvince);

        if (!provinceRegions.TryGetValue(pickupKey, out var pickupRegion))
        {
            return Result<RouteClassificationResult>.Failure(
                RouteClassificationErrors.ProvinceNotSupported(pickupProvince));
        }

        if (!provinceRegions.TryGetValue(deliveryKey, out var deliveryRegion))
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
