using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed class BrowserApiOpenApiTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (type.IsGenericType && type.GetGenericArguments() is [var keyType, var valueType] && keyType.IsEnum && valueType == typeof(int))
        {
            // Browser response dictionary values are JSON integers, not numeric request strings.
            schema.AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" };
        }
        if (type == typeof(decimal))
        {
            schema.Type = JsonSchemaType.String | (Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) is null ? 0 : JsonSchemaType.Null);
            schema.Format = null;
            schema.Pattern = BrowserApiJsonOptions.DecimalPattern;
        }
        if (type.IsEnum)
        {
            schema.Type = JsonSchemaType.String | (Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) is null ? 0 : JsonSchemaType.Null);
            schema.Format = null;
            schema.Enum = Enum.GetNames(type).Select(name => (JsonNode)JsonValue.Create(name)!).ToList();
        }
        return Task.CompletedTask;
    }
}
