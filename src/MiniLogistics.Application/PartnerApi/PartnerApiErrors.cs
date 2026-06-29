using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

public static class PartnerApiErrors
{
    public static readonly Error MissingApiKey = new(
        "PartnerApi.MissingApiKey",
        "API key is required.");

    public static readonly Error InvalidApiKey = new(
        "PartnerApi.InvalidApiKey",
        "API key is invalid.");

    public static readonly Error ApiClientInactive = new(
        "PartnerApi.ApiClientInactive",
        "API client is inactive.");

    public static readonly Error IdempotencyConflict = new(
        "PartnerApi.IdempotencyConflict",
        "Idempotency key was already used with a different request.");
}
