using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using MiniLogistics.Application.Authorization;
using MiniLogistics.Infrastructure.Identity;
using MiniLogistics.Web.Endpoints.Browser;

namespace MiniLogistics.Web.Services;

public sealed class BrowserSessionReader(UserManager<ApplicationUser> users)
{
    public async Task<BrowserSession?> ReadAsync(ClaimsPrincipal principal)
    {
        if (!BrowserUserClaims.TryGetUserId(principal, out var id)) return null;
        var user = await users.FindByIdAsync(id.ToString());
        return user is { IsActive: true } ? await ReadAsync(user) : null;
    }
    public async Task<BrowserSession> ReadAsync(ApplicationUser user)
    {
        var roles = (await users.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToArray();
        return new BrowserSession(user.Id, user.FullName, roles,
            roles.SelectMany(OperationPermissions.ForRole).Distinct().Order(StringComparer.Ordinal).ToArray(),
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en-US" : "vi-VN", ServerTimeZoneId);
    }
    public static string ServerTimeZoneId => TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id, out var iana)
        ? iana : TimeZoneInfo.Local.Id;
}
