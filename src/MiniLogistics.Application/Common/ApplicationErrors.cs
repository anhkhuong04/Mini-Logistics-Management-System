using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Common;

public static class ApplicationErrors
{
    public static Error ValidationFailed(string description) =>
        new("Application.ValidationFailed", description);

    public static Error Conflict(string description) =>
        new("Application.Conflict", description);

    public static Error NotFound(string description) =>
        new("Application.NotFound", description);

    public static Error Forbidden(string description) =>
        new("Application.Forbidden", description);
}
