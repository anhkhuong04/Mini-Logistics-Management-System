using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace MiniLogistics.Web.Services;

public static class BrowserSpaHostingExtensions
{
    public static IServiceCollection AddBrowserSpa(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddOptions<BrowserSpaOptions>()
            .Bind(configuration.GetSection(BrowserSpaOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.DistPath))
                {
                    var publishedPath = Path.Combine(environment.ContentRootPath, "frontend");
                    options.DistPath = environment.IsDevelopment() && !Directory.Exists(publishedPath)
                        ? Path.Combine(environment.ContentRootPath, "..", "MiniLogistics.Frontend", "dist")
                        : publishedPath;
                }
                options.DistPath = Path.GetFullPath(options.DistPath, environment.ContentRootPath);
            })
            .Validate(options => !options.ServeAtRoot,
                "BrowserSpa:ServeAtRoot is reserved for the root cutover; coexistence serves only /app.")
            .ValidateOnStart();
        return services;
    }

    public static WebApplication UseBrowserSpaAssets(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<BrowserSpaOptions>>().Value;
        if (!options.Enabled || !Directory.Exists(options.DistPath))
        {
            return app;
        }

        var provider = new PhysicalFileProvider(options.DistPath);
        app.Lifetime.ApplicationStopped.Register(provider.Dispose);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = provider,
            RequestPath = "/frontend-assets",
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.CacheControl =
                    context.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase)
                        ? "no-cache"
                        : "public,max-age=31536000,immutable";
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
            }
        });
        return app;
    }

    public static IEndpointRouteBuilder MapBrowserSpa(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<BrowserSpaOptions>>().Value;
        if (!options.Enabled)
        {
            return endpoints;
        }

        endpoints.MapGet("/frontend-config.js", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            var json = JsonSerializer.Serialize(new { basePath = "/app", assetBasePath = "/frontend-assets/" });
            return Results.Text($"window.__MINI_LOGISTICS_CONFIG__ = {json};", "text/javascript");
        }).AllowAnonymous().ExcludeFromDescription();

        endpoints.MapMethods("/app", ["GET", "HEAD"], ServeIndexAsync)
            .AllowAnonymous().ExcludeFromDescription();
        endpoints.MapMethods("/app/{**path:nonfile}", ["GET", "HEAD"], ServeIndexAsync)
            .AllowAnonymous().ExcludeFromDescription();
        return endpoints;
    }

    public static bool ShouldSkipStatusCodePage(PathString path) =>
        path.StartsWithSegments("/api")
        || path.StartsWithSegments("/auth")
        || path.StartsWithSegments("/shop/files")
        || path.StartsWithSegments("/culture")
        || path.StartsWithSegments("/health")
        || path.StartsWithSegments("/openapi")
        || path.StartsWithSegments("/frontend-assets")
        || path.StartsWithSegments("/app")
        || path.Equals("/frontend-config.js");

    private static async Task<IResult> ServeIndexAsync(
        HttpContext context,
        IOptions<BrowserSpaOptions> options)
    {
        var index = Path.Combine(options.Value.DistPath, "index.html");
        if (!File.Exists(index))
        {
            return Results.Text("Trang tạm thời chưa sẵn sàng. Vui lòng thử lại sau.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var html = await File.ReadAllTextAsync(index, context.RequestAborted);
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "vi";
        html = html.Replace("<html lang=\"vi\">", $"<html lang=\"{language}\">", StringComparison.OrdinalIgnoreCase);
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.Content(html, "text/html; charset=utf-8");
    }
}
