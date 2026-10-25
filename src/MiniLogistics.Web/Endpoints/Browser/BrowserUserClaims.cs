using System.Security.Claims;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserUserClaims
{
    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid id) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out id) && id != Guid.Empty;
}
