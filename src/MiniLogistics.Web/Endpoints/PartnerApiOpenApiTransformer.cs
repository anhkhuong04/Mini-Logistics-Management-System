using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MiniLogistics.Web.Endpoints;

public sealed class PartnerApiOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "MiniLogistics Partner API",
            Version = "v1",
            Description = "Shop-scoped quote, shipment creation, tracking and cancellation API."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["BearerApiKey"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "MiniLogistics API key",
                Description = "Use the environment-specific ml_test_ or ml_live_ API key."
            }
        };

        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations ?? []))
        {
            operation.Value.Security ??= [];
            operation.Value.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("BearerApiKey", document)] = []
            });

            if (operation.Value.OperationId == "PartnerCreateShipment")
            {
                operation.Value.Description =
                    "Requires CreateShipment scope and Idempotency-Key. Default quota: 30 requests/minute/API client. " +
                    "A Redis rate-store outage fails closed with HTTP 503 in production.";
                operation.Value.Parameters ??= [];
                operation.Value.Parameters.Add(new OpenApiParameter
                {
                    Name = "Idempotency-Key",
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = "Unique key for safely replaying a create request.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }
            else if (operation.Value.OperationId == "PartnerQuote")
            {
                operation.Value.Description =
                    "Requires Quote scope. Default quota: 60 requests/minute/API client; retry HTTP 429 using Retry-After.";
            }
            else if (operation.Value.OperationId == "PartnerTrackShipment")
            {
                operation.Value.Description =
                    "Requires TrackShipment scope. Reads any shipment in the authenticated shop; externalOrderId is null " +
                    "unless owned by this API client. Default quota: 120 requests/minute/API client.";
            }
            else if (operation.Value.OperationId == "PartnerCancelShipment")
            {
                operation.Value.Description =
                    "Requires CancelShipment scope and same-client shipment ownership. Default quota: 30 requests/minute/API client. " +
                    "A Redis rate-store outage fails closed with HTTP 503 in production.";
            }
        }

        return Task.CompletedTask;
    }
}
