using Microsoft.AspNetCore.Diagnostics;

namespace MiniLogistics.Web.Endpoints;

public sealed class PartnerApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<PartnerApiExceptionHandler> _logger;

    public PartnerApiExceptionHandler(ILogger<PartnerApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api") || httpContext.Response.HasStarted)
        {
            return false;
        }

        if (exception is BadHttpRequestException badRequestException)
        {
            var statusCode = badRequestException.StatusCode is >= 400 and < 500
                ? badRequestException.StatusCode
                : StatusCodes.Status400BadRequest;
            var isRequestTooLarge = statusCode == StatusCodes.Status413PayloadTooLarge;
            _logger.LogWarning(
                "Rejected malformed Partner API request with HTTP {StatusCode}. TraceId: {TraceId}",
                statusCode,
                httpContext.TraceIdentifier);

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(
                new PartnerApiErrorResponse(new PartnerApiError(
                    isRequestTooLarge ? "Request.TooLarge" : "Application.ValidationFailed",
                    isRequestTooLarge
                        ? "Request body exceeds the allowed size."
                        : "Request body contains invalid JSON.",
                    httpContext.TraceIdentifier)),
                cancellationToken);
            return true;
        }

        _logger.LogError(
            exception,
            "Unhandled exception in Partner API. TraceId: {TraceId}",
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                Error = new
                {
                    Code = "Internal.ServerError",
                    Message = "An unexpected error occurred. Please try again or contact support.",
                    TraceId = httpContext.TraceIdentifier
                }
            },
            cancellationToken);

        return true;
    }
}
