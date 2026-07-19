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
