using System.Security.Cryptography;
using System.Text;

namespace MiniLogistics.Application.PartnerApi;

public static class ApiKeyHasher
{
    public static string Hash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey.Trim()));
        return Convert.ToHexString(bytes);
    }

    public static string GetPrefix(string apiKey)
    {
        var normalized = apiKey.Trim();
        return normalized.Length <= 12
            ? normalized
            : normalized[..12];
    }
}
