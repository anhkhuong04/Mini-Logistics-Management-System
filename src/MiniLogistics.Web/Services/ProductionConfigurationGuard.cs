using MiniLogistics.Infrastructure.PartnerApi;

namespace MiniLogistics.Web.Services;

public static class ProductionConfigurationGuard
{
    public static void Validate(
        IConfiguration configuration,
        TrustedProxyOptions trustedProxyOptions,
        string rateLimitMode)
    {
        var errors = new List<string>();
        if (!string.Equals(configuration["PartnerApi:Environment"], "Live", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("PartnerApi:Environment must be Live in production.");
        }
        var publicBaseUrl = configuration["PublicBaseUrl"];
        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicUri)
            || publicUri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add("PublicBaseUrl must be an absolute HTTPS URL.");
        }

        if (trustedProxyOptions.ForwardLimit <= 0)
        {
            errors.Add("TrustedProxy:ForwardLimit must be greater than zero.");
        }

        if (trustedProxyOptions.KnownProxies.Length == 0
            && trustedProxyOptions.KnownNetworks.Length == 0)
        {
            errors.Add("At least one trusted reverse proxy or network must be configured.");
        }

        if (!string.Equals(rateLimitMode, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("PartnerApi:RateLimiting:Mode must be Redis in production.");
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")))
        {
            errors.Add("ConnectionStrings:Redis is required in production.");
        }

        var dataProtection = configuration
            .GetSection(DataProtectionStorageOptions.SectionName)
            .Get<DataProtectionStorageOptions>() ?? new DataProtectionStorageOptions();
        if (string.IsNullOrWhiteSpace(dataProtection.ApplicationName)
            || string.Equals(dataProtection.ApplicationName, "MiniLogistics", StringComparison.Ordinal))
        {
            errors.Add("DataProtection:ApplicationName must be explicit and environment-specific in production.");
        }

        if (string.IsNullOrWhiteSpace(dataProtection.KeysPath))
        {
            errors.Add("DataProtection:KeysPath must point to a persistent shared volume.");
        }

        if (string.IsNullOrWhiteSpace(dataProtection.CertificatePath))
        {
            errors.Add("DataProtection:CertificatePath is required to encrypt the shared key ring.");
        }

        var sqlConnection = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sqlConnection)
            || sqlConnection.Contains("localdb", StringComparison.OrdinalIgnoreCase)
            || sqlConnection.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("DefaultConnection must target production SQL Server, not a local database.");
        }

        if (string.Equals(configuration["AllowedHosts"], "*", StringComparison.Ordinal))
        {
            errors.Add("AllowedHosts must be restricted in production.");
        }

        var corsOrigins = configuration.GetSection("Cors:PartnerApi:AllowedOrigins").Get<string[]>() ?? [];
        if (corsOrigins.Any(origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("Every Partner API CORS origin must be an absolute HTTPS origin.");
        }

        if (configuration.GetValue<bool>("Seeding:Enabled"))
        {
            errors.Add("Seeding must be disabled in production.");
        }

        if (!configuration.GetValue<bool>("PartnerApi:Retention:Enabled"))
        {
            errors.Add("PartnerApi:Retention:Enabled must be true in production.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Production configuration validation failed: " + string.Join(" ", errors));
        }
    }
}
