using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MiniLogistics.Application.Common;

public sealed partial class PiiMaskingService : IPiiMaskingService
{
    private static readonly string[] SensitivePropertyFragments =
    [
        "phone", "address", "street", "apikey", "secret", "password", "token", "authorization"
    ];

    public string? MaskSensitiveJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        try
        {
            var node = JsonNode.Parse(json);
            MaskNode(node);
            return node?.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return MaskSensitiveText(json);
        }
    }

    public string? MaskSensitiveText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? text
            : PhonePattern().Replace(text, match => MaskPhone(match.Value));
    }

    public string MaskPhone(string phoneNumber)
    {
        var trimmed = phoneNumber.Trim();
        if (trimmed.Length <= 6)
        {
            return new string('*', trimmed.Length);
        }

        return string.Concat(trimmed.AsSpan(0, 3), new string('*', trimmed.Length - 6), trimmed.AsSpan(trimmed.Length - 3));
    }

    public string MaskName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return string.Join(' ', name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Length == 1 ? "*" : $"{part[0]}***"));
    }

    public string MaskAddress(string address) =>
        string.IsNullOrWhiteSpace(address) ? string.Empty : "***";

    private static void MaskNode(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToList())
            {
                if (SensitivePropertyFragments.Any(fragment =>
                        property.Key.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                {
                    jsonObject[property.Key] = "***";
                }
                else
                {
                    MaskNode(property.Value);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                MaskNode(item);
            }
        }
    }

    [GeneratedRegex(@"(?<!\d)(?:\+?84|0)\d{8,10}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}
