using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed class MiniLogisticsPartnerClient(HttpClient httpClient, string apiKey)
{
    public async Task<JsonDocument> TrackAsync(
        string trackingCode,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/partner/shipments/{Uri.EscapeDataString(trackingCode)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.ServiceUnavailable)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
            throw new PartnerRetryException(response.StatusCode, retryAfter);
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            using var error = JsonDocument.Parse(body);
            var value = error.RootElement.GetProperty("error");
            throw new InvalidOperationException(
                $"{value.GetProperty("code").GetString()}:{value.GetProperty("traceId").GetString()}");
        }

        return JsonDocument.Parse(body);
    }
}

public static class MiniLogisticsWebhookVerifier
{
    public static bool Verify(
        string secret,
        string timestamp,
        string suppliedSignature,
        ReadOnlySpan<byte> rawBody,
        DateTimeOffset nowUtc)
    {
        if (!DateTimeOffset.TryParse(timestamp, out var signedAtUtc)
            || (nowUtc - signedAtUtc).Duration() > TimeSpan.FromMinutes(5))
        {
            return false;
        }

        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var signedBytes = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(signedBytes, 0);
        rawBody.CopyTo(signedBytes.AsSpan(prefix.Length));
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signedBytes);
        var expected = Encoding.UTF8.GetBytes("sha256=" + Convert.ToHexString(digest).ToLowerInvariant());
        var supplied = Encoding.UTF8.GetBytes(suppliedSignature);
        return expected.Length == supplied.Length
            && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}

public sealed class PartnerRetryException(HttpStatusCode statusCode, TimeSpan retryAfter)
    : Exception($"Partner API returned {(int)statusCode}; retry after {retryAfter}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public TimeSpan RetryAfter { get; } = retryAfter;
}
