using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MiniLogistics.Web.Services;

public static class PartnerApiTelemetry
{
    public const string MeterName = "MiniLogistics.PartnerApi";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
        "partner_api.requests",
        description: "Partner API request count.");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "partner_api.request.duration",
        unit: "ms",
        description: "Partner API request duration.");
    private static readonly Counter<long> RateLimitStoreErrors = Meter.CreateCounter<long>(
        "partner_api.rate_limit_store.errors",
        description: "Partner API rate limit store failures.");
    private static readonly Counter<long> RateLimitCircuitOpen = Meter.CreateCounter<long>(
        "partner_api.rate_limit_store.circuit_open",
        description: "Requests handled while the Redis rate-limit circuit is open.");

    public static void RecordRequest(string method, string route, int statusCode, double durationMs)
    {
        var tags = new TagList
        {
            { "http.request.method", method },
            { "http.route", route },
            { "http.response.status_code", statusCode }
        };
        RequestCounter.Add(1, tags);
        RequestDuration.Record(durationMs, tags);
    }

    public static void RecordRateLimitStoreError(PartnerApiRateLimitKind kind)
    {
        RateLimitStoreErrors.Add(1, new KeyValuePair<string, object?>("partner_api.action", kind.ToString()));
    }

    public static void RecordRateLimitCircuitOpen(PartnerApiRateLimitKind kind)
    {
        RateLimitCircuitOpen.Add(1, new KeyValuePair<string, object?>("partner_api.action", kind.ToString()));
    }
}
