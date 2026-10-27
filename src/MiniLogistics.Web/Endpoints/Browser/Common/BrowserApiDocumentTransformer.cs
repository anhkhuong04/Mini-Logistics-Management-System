using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed class BrowserApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "MiniLogistics Browser API";
        document.Info.Description = "Same-origin Identity cookie. Mutations require X-CSRF-TOKEN from auth/csrf; refresh it after identity changes. Errors use application/problem+json.";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["IdentityCookie"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Name = ".AspNetCore.Identity.Application",
                Description = "HttpOnly Identity cookie managed by the browser; never persist credentials or tokens in localStorage."
            }
        };
        foreach (var (path, item) in document.Paths)
            foreach (var (method, operation) in item.Operations ?? [])
            {
                if (path.StartsWith("/api/v1/app/") && operation.OperationId is not ("BrowserCsrf" or "BrowserLogin" or "BrowserRegisterShop"))
                    operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("IdentityCookie", document)] = [] }];
                if (method != HttpMethod.Get && path.StartsWith("/api/v1/app/"))
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = "X-CSRF-TOKEN",
                        In = ParameterLocation.Header,
                        Required = true,
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    });
                }
            }
        return Task.CompletedTask;
    }
}
