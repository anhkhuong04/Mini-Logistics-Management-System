using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace MiniLogistics.Web.Services;

public sealed record RegistrationDraft(
    string FullName,
    string Email,
    string ShopName,
    string PhoneNumber,
    string AddressLine,
    string Ward,
    string Province);

public static class RegistrationDraftCookie
{
    private const string CookieName = "lak.registration-draft";
    private const string ProtectorPurpose = "MiniLogistics.Web.RegistrationDraft.v1";

    public static void Store(
        HttpResponse response,
        IDataProtectionProvider dataProtectionProvider,
        RegistrationDraft draft)
    {
        var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        var protectedDraft = protector.Protect(JsonSerializer.Serialize(draft));

        response.Cookies.Append(CookieName, protectedDraft, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromMinutes(15),
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = true
        });
    }

    public static RegistrationDraft? TryRead(
        HttpRequest? request,
        IDataProtectionProvider dataProtectionProvider)
    {
        if (request is null || !request.Cookies.TryGetValue(CookieName, out var protectedDraft))
        {
            return null;
        }

        try
        {
            var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
            return JsonSerializer.Deserialize<RegistrationDraft>(protector.Unprotect(protectedDraft));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return null;
        }
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = true
        });
    }
}
