namespace MiniLogistics.Infrastructure.PartnerApi;

public sealed class PartnerApiRetentionOptions
{
    public const string SectionName = "PartnerApi:Retention";

    public bool Enabled { get; init; }

    public bool RunOnStartup { get; init; }

    public int IntervalHours { get; init; } = 6;

    public int SucceededQueueRetentionDays { get; init; } = 30;

    public int RequestAuditRetentionDays { get; init; } = 180;

    public int CredentialAuditRetentionDays { get; init; } = 365;

    public int BatchSize { get; init; } = 1000;
}
