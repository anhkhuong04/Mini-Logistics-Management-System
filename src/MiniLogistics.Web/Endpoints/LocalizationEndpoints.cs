using Microsoft.AspNetCore.Localization;

namespace MiniLogistics.Web.Endpoints;

public static class LocalizationEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> SupportedCultures =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["vi"] = "vi-VN",
            ["vi-VN"] = "vi-VN",
            ["en"] = "en-US",
            ["en-US"] = "en-US"
        };

    public static IEndpointRouteBuilder MapLocalizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/culture/set", SetCulture)
            .AllowAnonymous()
            .ExcludeFromDescription();
        return endpoints;
    }

    private static IResult SetCulture(string culture, string? returnUrl, HttpContext context)
    {
        var selectedCulture = SupportedCultures.GetValueOrDefault(culture, "vi-VN");
        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selectedCulture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Path = "/"
            });

        return Results.LocalRedirect(IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    private static bool IsLocalUrl(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value[0] == '/'
            && (value.Length == 1 || (value[1] != '/' && value[1] != '\\'));
    }
}
