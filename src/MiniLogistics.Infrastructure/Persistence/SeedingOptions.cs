namespace MiniLogistics.Infrastructure.Persistence;

public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    public bool Enabled { get; init; }

    public string DemoPartnerApiKey { get; init; } = string.Empty;

    public string DemoAdminPassword { get; init; } = string.Empty;

    public string DemoShopPassword { get; init; } = string.Empty;

    public string DemoShipperPassword { get; init; } = string.Empty;

    public string DemoOperatorPassword { get; init; } = string.Empty;

    public bool HasRequiredDemoCredentials()
    {
        return Enabled
            && !string.IsNullOrWhiteSpace(DemoPartnerApiKey)
            && !string.IsNullOrWhiteSpace(DemoAdminPassword)
            && !string.IsNullOrWhiteSpace(DemoShopPassword)
            && !string.IsNullOrWhiteSpace(DemoShipperPassword)
            && !string.IsNullOrWhiteSpace(DemoOperatorPassword);
    }
}
