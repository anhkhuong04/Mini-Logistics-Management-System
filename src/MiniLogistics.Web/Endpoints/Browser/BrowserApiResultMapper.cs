using MiniLogistics.Domain.Common;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserApiProblem(string Type, string Title, int Status, string Detail, string Code,
    string TraceId, IReadOnlyDictionary<string, string[]>? Errors = null);

public static class BrowserApiResultMapper
{
    public static int StatusFor(string code) => code switch
    {
        "Auth.InvalidCredentials" or "Browser.Unauthenticated" => 401,
        "Auth.LockedOut" => 423,
        "Application.Forbidden" => 403,
        "Application.NotFound" => 404,
        "Application.Conflict" or "Application.ConcurrencyConflict" or "Application.CapacityReached" => 409,
        "Shipment.CannotEditBeforePickup" or "Shipment.OnlyDraftCanBeSubmitted" or "Shipment.CannotCancel"
            or "Shipment.InvalidStatusTransition" or "Shipment.CompletedShipmentCannotChange" => 409,
        "Browser.RateLimitExceeded" => 429,
        "Application.Unavailable" or "Application.RegistrationFailed" => 503,
        "Browser.InternalError" => 500,
        _ => 400
    };
    public static IResult Problem(HttpContext context, string code, int? status = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var httpStatus = status ?? StatusFor(code);
        var title = httpStatus switch
        {
            400 => "Invalid request", 401 => "Authentication required", 403 => "Access denied",
            404 => "Resource not found", 409 => "Reload the current state before trying again",
            423 => "Account temporarily locked", 429 => "Too many requests", 503 => "Service unavailable",
            _ => "An unexpected error occurred"
        };
        context.Response.Headers.CacheControl = "no-store";
        if (httpStatus == 429 && !context.Response.Headers.ContainsKey("Retry-After")) context.Response.Headers.RetryAfter = "60";
        return Results.Json(new BrowserApiProblem("about:blank", title, httpStatus, title, code,
            context.TraceIdentifier, errors), BrowserApiJsonOptions.Value, "application/problem+json", httpStatus);
    }
    public static IResult From<T>(HttpContext context, Result<T> result) => result.IsSuccess
        ? Json(result.Value) : Problem(context, result.Error.Code);
    public static IResult Json<T>(T value) => Results.Json(value, BrowserApiJsonOptions.Value);
}
