using FluentValidation;
using MiniLogistics.Web.Services;
using MiniLogistics.Application.Shops.GetShopProfile;
using MiniLogistics.Application.Shops.UpdateShopProfile;
using MiniLogistics.Application.Shops.CreateAdditionalShop;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserShopProfileRequest(string Name, string PhoneNumber, string AddressLine, string Ward, string Province, string Country = "Vietnam");
public static class BrowserShopProfileEndpoints
{
    public static void MapBrowserShopProfile(this RouteGroupBuilder group)
    {
        group.MapGet("/shops/{shopId:guid}/profile", async (HttpContext context, Guid shopId, IGetShopProfileService service) => {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            return BrowserApiResultMapper.From(context, await service.GetAsync(actor, shopId, context.RequestAborted));
        }).WithName("BrowserShopProfile").Produces<GetShopProfileResponse>();
        group.MapPut("/shops/{shopId:guid}/profile", async (HttpContext context, Guid shopId, IUpdateShopProfileService service, IValidator<UpdateShopProfileCommand> validator) => {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var body = await BrowserApiEndpoints.ReadAsync<BrowserShopProfileRequest>(context);
            var command = new UpdateShopProfileCommand(actor, body.Name, body.PhoneNumber, body.AddressLine, body.Ward, body.Province, body.Country, shopId);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors)
                : BrowserApiResultMapper.From(context, await service.UpdateAsync(command, context.RequestAborted));
        }).WithName("BrowserUpdateShopProfile").Accepts<BrowserShopProfileRequest>("application/json").Produces<UpdateShopProfileResponse>();
        group.MapPost("/shops", async (HttpContext context, ICreateAdditionalShopService service, IValidator<CreateAdditionalShopCommand> validator) => {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var body = await BrowserApiEndpoints.ReadAsync<BrowserShopProfileRequest>(context);
            var command = new CreateAdditionalShopCommand(actor, body.Name, body.PhoneNumber, body.AddressLine, body.Ward, body.Province, body.Country);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            if (!validation.IsValid) return BrowserValidationProblem.From(context, validation.Errors);
            var result = await service.CreateAsync(command, context.RequestAborted);
            if (result.IsFailure) return BrowserApiResultMapper.Problem(context, result.Error.Code);
            context.Response.Headers.Location = $"/api/v1/app/shops/{result.Value.ShopId}/profile";
            return Results.Json(result.Value, BrowserApiJsonOptions.Value, statusCode: 201);
        }).WithName("BrowserCreateAdditionalShop").Accepts<BrowserShopProfileRequest>("application/json").Produces<CreateAdditionalShopResponse>(201);
    }
}
