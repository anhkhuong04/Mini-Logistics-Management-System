namespace MiniLogistics.Infrastructure.PartnerApi;

public sealed class DataProtectionStorageOptions
{
    public const string SectionName = "DataProtection";

    public string ApplicationName { get; init; } = "MiniLogistics";

    public string KeysPath { get; init; } = string.Empty;

    public string CertificatePath { get; init; } = string.Empty;

    public string CertificatePassword { get; init; } = string.Empty;

    public int KeyLifetimeDays { get; init; } = 90;
}
