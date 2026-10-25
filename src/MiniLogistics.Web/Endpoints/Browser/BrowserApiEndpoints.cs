using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserApiEndpoints
{
    public static bool IsBrowserPath(PathString path) => path.StartsWithSegments("/api/v1/app")
        || path.StartsWithSegments("/api/v1/public");

    public static async Task<T> ReadAsync<T>(HttpContext context) where T : class
    {
        if (!context.Request.HasJsonContentType()) throw new BadHttpRequestException("Expected JSON.");
        return await context.Request.ReadFromJsonAsync<T>(BrowserApiJsonOptions.Value, context.RequestAborted)
            ?? throw new JsonException();
    }

    public static RouteGroupBuilder MapBrowserApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/app").WithGroupName("browser-v1").RequireAuthorization();
        foreach (var status in new[] { 400, 401, 403, 404, 409, 413, 415, 423, 429, 500, 503 })
            group.WithMetadata(new Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute(typeof(BrowserApiProblem), status, "application/problem+json"));
        group.AddEndpointFilter(async (invocation, next) =>
        {
            var context = invocation.HttpContext;
            context.Response.Headers.CacheControl = "no-store";
            var bodyLimit = context.GetEndpoint()?.Metadata.GetMetadata<BrowserRequestBodyLimit>()?.Bytes ?? 64 * 1024;
            if (context.Request.ContentLength > bodyLimit) return BrowserApiResultMapper.Problem(context, "Request.TooLarge", 413);
            var bodySize = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (bodySize is { IsReadOnly: false }) bodySize.MaxRequestBodySize = bodyLimit;
            if (context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is null)
            {
                if (!BrowserUserClaims.TryGetUserId(context.User, out _)) return BrowserApiResultMapper.Problem(context, "Browser.Unauthenticated", 401);
                if (await context.RequestServices.GetRequiredService<MiniLogistics.Web.Services.BrowserSessionReader>().ReadAsync(context.User) is null)
                    return BrowserApiResultMapper.Problem(context, "Application.Forbidden", 403);
            }
            if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method)
                || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
            {
                try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
                catch (AntiforgeryValidationException)
                { return BrowserApiResultMapper.Problem(context, "Browser.CsrfValidationFailed", 400); }
            }
            return await next(invocation);
        });
        group.MapBrowserAuthentication();
        group.MapBrowserShopContext();
        group.MapBrowserShopShipmentReads();
        group.MapBrowserShopReporting();
        group.MapBrowserShippingQuote();
        group.MapBrowserShopShipmentCommands();
        group.MapBrowserShipmentImports();
        endpoints.MapBrowserReferenceData();
        return group;
    }
}
