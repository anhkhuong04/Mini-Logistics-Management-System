using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserApiJsonOptions
{
    public const string DecimalPattern = @"^-?[0-9]+(?:\.[0-9]+)?$";
    public static JsonSerializerOptions Value { get; } = Create();
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new DecimalConverter());
        options.Converters.Add(new UtcConverter());
        options.Converters.Add(new StrictEnumFactory());
        return options;
    }

    private sealed class DecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var text = reader.GetString()!;
            if (!Regex.IsMatch(text, DecimalPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
                || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var value)) throw new JsonException();
            var parts = text.TrimStart('-').Split('.');
            var whole = parts[0].TrimStart('0');
            if (whole.Length == 0) whole = "0";
            var fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : "";
            var canonical = whole + (fraction.Length == 0 ? "" : "." + fraction);
            if (text.StartsWith('-') && canonical != "0") canonical = "-" + canonical;
            // decimal.TryParse can silently round excess scale; the browser contract must reject loss of precision.
            if (value.ToString("0.############################", CultureInfo.InvariantCulture) != canonical) throw new JsonException();
            return value;
        }
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class UtcConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var text = reader.GetString();
            if (text is null || !(text.EndsWith('Z') || Regex.IsMatch(text, @"[+-]\d{2}:\d{2}$"))
                || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
                throw new JsonException();
            return value.ToUniversalTime();
        }
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime());
    }

    private sealed class StrictEnumFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsEnum;
        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(StrictEnumConverter<>).MakeGenericType(type))!;
    }
    private sealed class StrictEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var name = reader.GetString();
            if (!Enum.GetNames<T>().Contains(name, StringComparer.Ordinal) || !Enum.TryParse<T>(name, out var value)) throw new JsonException();
            return value;
        }
        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (!Enum.IsDefined(value)) throw new JsonException();
            writer.WritePropertyName(value.ToString());
        }
        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var text = reader.GetString();
            if (!Enum.GetNames<T>().Contains(text, StringComparer.Ordinal)
                || !Enum.TryParse<T>(text, out var value)) throw new JsonException();
            return value;
        }
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (!Enum.IsDefined(value)) throw new JsonException();
            writer.WriteStringValue(value.ToString());
        }
    }
}
