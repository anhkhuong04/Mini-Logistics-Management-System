using Microsoft.AspNetCore.Identity;
using MiniLogistics.Application.Shops.RegisterShop;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Web.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/register-shop", RegisterShopAsync);
        endpoints.MapPost("/auth/login", LoginAsync);
        endpoints.MapPost("/auth/logout", LogoutAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterShopAsync(
        HttpContext httpContext,
        IRegisterShopService registerShopService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
        var command = new RegisterShopCommand(
            GetValue(form, "FullName"),
            GetValue(form, "Email"),
            GetValue(form, "Password"),
            GetValue(form, "ShopName"),
            GetValue(form, "PhoneNumber"),
            GetValue(form, "AddressLine"),
            GetValue(form, "Ward"),
            GetValue(form, "Province"),
            GetValue(form, "Country", "Vietnam"));

        var result = await registerShopService.RegisterAsync(command, httpContext.RequestAborted);
        if (result.IsFailure)
        {
            return RedirectWithError("/register-shop", result.Error.Description);
        }

        var user = await userManager.FindByIdAsync(result.Value.UserId.ToString());
        if (user is null)
        {
            return RedirectWithError("/login", "Account was created. Please sign in.");
        }

        await signInManager.SignInAsync(user, isPersistent: false);

        return Results.Redirect("/dashboard");
    }

    private static async Task<IResult> LoginAsync(
        HttpContext httpContext,
        SignInManager<ApplicationUser> signInManager)
    {
        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
        var email = GetValue(form, "Email");
        var password = GetValue(form, "Password");
        var rememberMe = string.Equals(GetValue(form, "RememberMe"), "on", StringComparison.OrdinalIgnoreCase);

        var result = await signInManager.PasswordSignInAsync(
            email,
            password,
            rememberMe,
            lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            return RedirectWithError("/login", "Email or password is incorrect.");
        }

        return Results.Redirect("/dashboard");
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();

        return Results.Redirect("/login");
    }

    private static string GetValue(IFormCollection form, string key, string fallback = "")
    {
        return form.TryGetValue(key, out var values)
            ? values.ToString().Trim()
            : fallback;
    }

    private static IResult RedirectWithError(string path, string error)
    {
        var encodedError = Uri.EscapeDataString(error);

        return Results.Redirect($"{path}?error={encodedError}");
    }
}
