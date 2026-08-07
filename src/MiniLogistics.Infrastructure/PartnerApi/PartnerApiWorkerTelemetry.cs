using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MiniLogistics.Infrastructure.PartnerApi;

public static class PartnerApiWorkerTelemetry
{
    public const string MeterName = "MiniLogistics.PartnerWorkers";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Claimed = Meter.CreateCounter<long>(
        "partner_worker.claimed",
        description: "Rows claimed by outbox and webhook workers.");
    private static readonly Counter<long> Results = Meter.CreateCounter<long>(
        "partner_worker.results",
        description: "Worker processing results by queue and status.");
    private static readonly Histogram<double> QueueAge = Meter.CreateHistogram<double>(
        "partner_worker.queue.age",
        unit: "s",
        description: "Age of a row when it is claimed.");
    private static readonly Histogram<double> DeliveryDuration = Meter.CreateHistogram<double>(
        "partner_webhook.delivery.duration",
        unit: "ms",
        description: "Webhook delivery attempt duration.");
    private static readonly Counter<long> RetentionDeleted = Meter.CreateCounter<long>(
        "partner_worker.retention.deleted",
        description: "Rows removed by the Partner API retention worker.");

    public static void RecordClaim(string queue, DateTimeOffset createdAtUtc, DateTimeOffset claimedAtUtc)
    {
        var tags = new TagList { { "queue", queue } };
        Claimed.Add(1, tags);
        QueueAge.Record(Math.Max(0, (claimedAtUtc - createdAtUtc).TotalSeconds), tags);
    }

    public static void RecordResult(string queue, string status, long? durationMs = null)
    {
        var tags = new TagList
        {
            { "queue", queue },
            { "status", status }
        };
        Results.Add(1, tags);
        if (durationMs.HasValue)
        {
            DeliveryDuration.Record(durationMs.Value, tags);
        }
    }

    public static void RecordRetention(string table, int deletedRows)
    {
        if (deletedRows > 0)
        {
            RetentionDeleted.Add(deletedRows, new KeyValuePair<string, object?>("table", table));
        }
    }
}
