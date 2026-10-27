using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed class BrowserApiExceptionHandler(ILogger<BrowserApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (!BrowserApiEndpoints.IsBrowserPath(context.Request.Path) || context.Response.HasStarted)
            return false;
        var status = exception switch
        {
            JsonException => 400,
            BadHttpRequestException bad => bad.StatusCode,
            DbUpdateConcurrencyException => 409,
            MiniLogistics.Application.Common.ConcurrencyConflictException => 409,
            _ => 500
        };
        // Do not log exception messages/bodies: JSON/Identity errors may contain credentials or PII.
        logger.LogWarning("Browser request failed with HTTP {Status}; trace {TraceId}", status, context.TraceIdentifier);
        await BrowserApiResultMapper.Problem(context, status switch
        {
            409 => "Application.ConcurrencyConflict",
            500 => "Browser.InternalError",
            _ => "Application.ValidationFailed"
        }, status).ExecuteAsync(context);
        return true;
    }
}
