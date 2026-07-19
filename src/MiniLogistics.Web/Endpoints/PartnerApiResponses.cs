namespace MiniLogistics.Web.Endpoints;

public sealed record PartnerApiErrorResponse(PartnerApiError Error);

public sealed record PartnerApiError(
    string Code,
    string Message,
    string TraceId);
