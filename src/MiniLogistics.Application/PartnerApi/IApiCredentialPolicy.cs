using System.Security.Cryptography;

namespace MiniLogistics.Application.PartnerApi;

public interface IApiCredentialPolicy
{
    string ApiKeyPrefix { get; }

    string GenerateApiKey();

    bool IsAllowed(string apiKey);
}

public sealed class EnvironmentApiCredentialPolicy : IApiCredentialPolicy
{
    public EnvironmentApiCredentialPolicy(string environment)
    {
        ApiKeyPrefix = environment switch
        {
            var value when value.Equals("Live", StringComparison.OrdinalIgnoreCase) => "ml_live_",
            var value when value.Equals("Sandbox", StringComparison.OrdinalIgnoreCase) => "ml_test_",
            _ => throw new ArgumentException(
                "Partner API environment must be either 'Live' or 'Sandbox'.",
                nameof(environment))
        };
    }

    public string ApiKeyPrefix { get; }

    public string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return ApiKeyPrefix + Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    public bool IsAllowed(string apiKey)
    {
        return apiKey.StartsWith(ApiKeyPrefix, StringComparison.Ordinal);
    }
}
