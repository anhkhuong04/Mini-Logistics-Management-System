using System.Security.Claims;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserUserClaims
{
    public static Guid GetUserId(HttpContext context) =>
        TryGetUserId(context.User, out var id) ? id : Guid.Empty;

    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid id) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out id) && id != Guid.Empty;
}
