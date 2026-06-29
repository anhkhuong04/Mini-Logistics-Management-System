using System.Security.Cryptography;
using System.Text;

namespace MiniLogistics.Application.PartnerApi;

public static class WebhookSignature
{
    public static string Compute(
        string signingSecret,
        string timestamp,
        string payloadJson)
    {
        var message = $"{timestamp}.{payloadJson}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));

        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
