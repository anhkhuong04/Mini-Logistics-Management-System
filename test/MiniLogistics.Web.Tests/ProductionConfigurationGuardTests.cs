using Microsoft.Extensions.Configuration;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class ProductionConfigurationGuardTests
{
    [Fact]
    public void Validate_WithProductionDependenciesConfigured_Succeeds()
    {
        var configuration = CreateConfiguration();

        ProductionConfigurationGuard.Validate(
            configuration,
            new TrustedProxyOptions
            {
                KnownNetworks = ["10.20.0.0/16"],
                ForwardLimit = 2
            },
            "Redis");
    }

    [Fact]
    public void Validate_WithDevelopmentDefaults_RejectsStartup()
    {
        var configuration = CreateConfiguration();
        configuration["PartnerApi:Environment"] = "Sandbox";
        configuration["DataProtection:ApplicationName"] = "MiniLogistics";
        configuration["DataProtection:KeysPath"] = string.Empty;
        configuration["ConfigurationCache:KeyPrefix"] = string.Empty;
        configuration["AllowedHosts"] = "*";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationGuard.Validate(
                configuration,
                new TrustedProxyOptions(),
                "Memory"));

        Assert.Contains("PartnerApi:Environment", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ConfigurationCache:KeyPrefix", exception.Message, StringComparison.Ordinal);
        Assert.Contains("RateLimiting:Mode", exception.Message, StringComparison.Ordinal);
        Assert.Contains("DataProtection:ApplicationName", exception.Message, StringComparison.Ordinal);
        Assert.Contains("DataProtection:KeysPath", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AllowedHosts", exception.Message, StringComparison.Ordinal);
        Assert.Contains("trusted reverse proxy", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ConfigurationManager CreateConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration["PublicBaseUrl"] = "https://api.minilogistics.example";
        configuration["PartnerApi:Environment"] = "Live";
        configuration["ConnectionStrings:DefaultConnection"] = "Server=sql.internal;Database=MiniLogisticsProduction;User Id=app;Password=secret";
        configuration["ConnectionStrings:Redis"] = "redis.internal:6379";
        configuration["ConfigurationCache:KeyPrefix"] = "mini-logistics:production:configuration";
        configuration["ConfigurationCache:ConsistencyWindowSeconds"] = "15";
        configuration["DataProtection:ApplicationName"] = "MiniLogistics-Production";
        configuration["DataProtection:KeysPath"] = "/mnt/keys";
        configuration["DataProtection:CertificatePath"] = "/mnt/secrets/key-ring.pfx";
        configuration["AllowedHosts"] = "api.minilogistics.example";
        configuration["Cors:PartnerApi:AllowedOrigins:0"] = "https://shop.example";
        configuration["Seeding:Enabled"] = "false";
        configuration["PartnerApi:Retention:Enabled"] = "true";
        return configuration;
    }
}
