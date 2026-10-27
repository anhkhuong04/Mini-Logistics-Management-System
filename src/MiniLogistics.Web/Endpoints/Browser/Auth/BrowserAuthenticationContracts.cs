namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserSession(Guid UserId, string DisplayName, string[] Roles,
    string[] OperationPermissions, string Locale, string ServerTimeZoneId);
public sealed record BrowserCsrfResponse(string RequestToken);
public sealed record BrowserLoginRequest(string Email, string Password, bool RememberMe = false);
