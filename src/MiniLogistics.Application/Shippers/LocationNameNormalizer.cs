using System.Globalization;
using System.Text;

namespace MiniLogistics.Application.Shippers;

internal static class LocationNameNormalizer
{
    public static string NormalizeProvince(string value)
    {
        var normalized = Normalize(value);

        normalized = normalized
            .Replace("thanh pho ", string.Empty, StringComparison.Ordinal)
            .Replace("tinh ", string.Empty, StringComparison.Ordinal);

        return CollapseWhitespace(normalized);
    }

    public static string NormalizeAreaValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : CollapseWhitespace(Normalize(value));
    }

    private static string Normalize(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormD)
            .Trim()
            .ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('\u0111', 'd')
            .Replace('\u0110', 'D');
    }

    private static string CollapseWhitespace(string value)
    {
        return string.Join(
            ' ',
            value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
