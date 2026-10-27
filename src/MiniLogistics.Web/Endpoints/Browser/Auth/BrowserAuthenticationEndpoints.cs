using MiniLogistics.Infrastructure.ReferenceData;
using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using MiniLogistics.Application.Shops.RegisterShop;
using MiniLogistics.Infrastructure.Identity;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserAuthenticationEndpoints
{
    public static void MapBrowserAuthentication(this RouteGroupBuilder group)
    {
        group.MapGet("/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            BrowserApiResultMapper.Json(new BrowserCsrfResponse(antiforgery.GetAndStoreTokens(context).RequestToken!)))
            .AllowAnonymous().Produces<BrowserCsrfResponse>().WithName("BrowserCsrf");
        group.MapGet("/auth/me", async (HttpContext context, BrowserSessionReader reader) =>
        {
            var session = await reader.ReadAsync(context.User);
            return session is null ? BrowserApiResultMapper.Problem(context, "Application.Forbidden", 403)
                : BrowserApiResultMapper.Json(session);
        }).Produces<BrowserSession>().WithName("BrowserSession");
        group.MapPost("/auth/login", LoginAsync).AllowAnonymous().Accepts<BrowserLoginRequest>("application/json")
            .Produces<BrowserSession>().WithName("BrowserLogin");
        group.MapPost("/auth/register-shop", RegisterAsync).AllowAnonymous().Accepts<RegisterShopCommand>("application/json")
            .Produces<BrowserSession>().WithName("BrowserRegisterShop");
        group.MapPost("/auth/logout", async (SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).WithName("BrowserLogout").Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> LoginAsync(HttpContext context, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn, BrowserSessionReader reader)
    {
        var request = await BrowserApiEndpoints.ReadAsync<BrowserLoginRequest>(context);
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || string.IsNullOrEmpty(request.Password)
            || request.Password.Length > 100)
            return BrowserApiResultMapper.Problem(context, "Application.ValidationFailed", 400);
        var user = await users.FindByEmailAsync(request.Email.Trim()) ?? await users.FindByNameAsync(request.Email.Trim());
        // PasswordSignInAsync owns password verification and the existing lockout policy.
        if (user is null)
            return BrowserApiResultMapper.Problem(context, "Auth.InvalidCredentials");
        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
            return BrowserApiResultMapper.Problem(context, "Auth.LockedOut");
        if (!check.Succeeded)
            return BrowserApiResultMapper.Problem(context, "Auth.InvalidCredentials");
        if (!user.IsActive)
            return BrowserApiResultMapper.Problem(context, "Application.Forbidden");
        await signIn.SignInAsync(user, request.RememberMe);
        return BrowserApiResultMapper.Json(await reader.ReadAsync(user));
    }

    private static async Task<IResult> RegisterAsync(HttpContext context, IValidator<RegisterShopCommand> validator,
        IRegisterShopService register, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        BrowserSessionReader reader, VietnamAdministrativeDivisionService divisions)
    {
        var command = await BrowserApiEndpoints.ReadAsync<RegisterShopCommand>(context);
        var validation = await validator.ValidateAsync(command, context.RequestAborted);
        if (!validation.IsValid)
            return BrowserValidationProblem.From(context, validation.Errors);
        var address = await divisions.NormalizeProvinceWardAsync(command.Province, command.Ward, context.RequestAborted);
        if (address.IsFailure)
            return BrowserApiResultMapper.Problem(context, address.Error.Code, 400,
            new Dictionary<string, string[]> { ["ward"] = ["Choose a valid province and ward."] });
        command = command with
        {
            Province = address.Value.Province,
            Ward = address.Value.Ward
        };
        var result = await register.RegisterAsync(command, context.RequestAborted);
        if (result.IsFailure)
            return BrowserApiResultMapper.Problem(context, result.Error.Code);
        var user = await users.FindByIdAsync(result.Value.UserId.ToString());
        if (user is not { IsActive: true })
            return BrowserApiResultMapper.Problem(context, "Application.Unavailable");
        await signIn.SignInAsync(user, isPersistent: false);
        return BrowserApiResultMapper.Json(await reader.ReadAsync(user));
    }
}
