using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniLogistics.Application.PartnerApi;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class PartnerWebhookReceiverFixtureTests
{
    private const string Secret = "receiver-fixture-secret-at-least-32-bytes";
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Receive_ValidNewEvent_AppliesShipmentStatus()
    {
        var receiver = new IdempotentWebhookReceiver(Secret);
        var request = CreateRequest(Guid.NewGuid(), "InTransit", Now.AddMinutes(-1));

        var result = receiver.Receive(request, Now);

        Assert.Equal(WebhookReceiveResult.Applied, result);
        Assert.Equal("InTransit", receiver.StatusByTrackingCode["ML202608020001"].Status);
    }

    [Fact]
    public void Receive_DuplicateEvent_IsAcknowledgedWithoutApplyingTwice()
    {
        var receiver = new IdempotentWebhookReceiver(Secret);
        var eventId = Guid.NewGuid();
        var request = CreateRequest(eventId, "InTransit", Now.AddMinutes(-1));

        Assert.Equal(WebhookReceiveResult.Applied, receiver.Receive(request, Now));
        Assert.Equal(WebhookReceiveResult.Duplicate, receiver.Receive(request, Now));
        Assert.Single(receiver.ProcessedEventIds);
    }

    [Fact]
    public void Receive_OlderEvent_IsAcknowledgedWithoutRegressingStatus()
    {
        var receiver = new IdempotentWebhookReceiver(Secret);
        var newer = CreateRequest(Guid.NewGuid(), "Delivering", Now.AddMinutes(-1));
        var older = CreateRequest(Guid.NewGuid(), "InTransit", Now.AddMinutes(-2));

        Assert.Equal(WebhookReceiveResult.Applied, receiver.Receive(newer, Now));
        Assert.Equal(WebhookReceiveResult.OutOfOrder, receiver.Receive(older, Now));
        Assert.Equal("Delivering", receiver.StatusByTrackingCode["ML202608020001"].Status);
    }

    [Fact]
    public void Receive_InvalidSignatureOrStaleTimestamp_IsRejected()
    {
        var receiver = new IdempotentWebhookReceiver(Secret);
        var valid = CreateRequest(Guid.NewGuid(), "InTransit", Now.AddMinutes(-1));
        var tampered = valid with { Signature = "sha256=" + new string('0', 64) };
        var stale = CreateRequest(Guid.NewGuid(), "InTransit", Now.AddMinutes(-6));

        Assert.Equal(WebhookReceiveResult.Unauthorized, receiver.Receive(tampered, Now));
        Assert.Equal(WebhookReceiveResult.Unauthorized, receiver.Receive(stale, Now));
        Assert.Empty(receiver.ProcessedEventIds);
    }

    private static SignedWebhookRequest CreateRequest(
        Guid eventId,
        string status,
        DateTimeOffset changedAtUtc)
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventId,
            @event = WebhookEventTypes.ShipmentStatusChanged,
            trackingCode = "ML202608020001",
            externalOrderId = "ORDER-10001",
            status,
            changedAtUtc,
            schemaVersion = "1.0"
        });
        var timestamp = changedAtUtc.ToString("O");
        return new SignedWebhookRequest(
            timestamp,
            WebhookSignature.Compute(Secret, timestamp, payload),
            payload);
    }

    private sealed class IdempotentWebhookReceiver(string secret)
    {
        public HashSet<Guid> ProcessedEventIds { get; } = [];
        public Dictionary<string, ShipmentProjection> StatusByTrackingCode { get; } = [];

        public WebhookReceiveResult Receive(SignedWebhookRequest request, DateTimeOffset nowUtc)
        {
            if (!DateTimeOffset.TryParse(request.Timestamp, out var signedAtUtc)
                || (nowUtc - signedAtUtc).Duration() > TimeSpan.FromMinutes(5)
                || !HasValidSignature(request))
            {
                return WebhookReceiveResult.Unauthorized;
            }

            using var document = JsonDocument.Parse(request.RawBody);
            var root = document.RootElement;
            var eventId = root.GetProperty("eventId").GetGuid();
            if (!ProcessedEventIds.Add(eventId))
            {
                return WebhookReceiveResult.Duplicate;
            }

            var trackingCode = root.GetProperty("trackingCode").GetString()!;
            var changedAtUtc = root.GetProperty("changedAtUtc").GetDateTimeOffset();
            if (StatusByTrackingCode.TryGetValue(trackingCode, out var current)
                && changedAtUtc <= current.ChangedAtUtc)
            {
                return WebhookReceiveResult.OutOfOrder;
            }

            StatusByTrackingCode[trackingCode] = new ShipmentProjection(
                root.GetProperty("status").GetString()!,
                changedAtUtc);
            return WebhookReceiveResult.Applied;
        }

        private bool HasValidSignature(SignedWebhookRequest request)
        {
            var expected = Encoding.UTF8.GetBytes(
                WebhookSignature.Compute(secret, request.Timestamp, request.RawBody));
            var supplied = Encoding.UTF8.GetBytes(request.Signature);
            return expected.Length == supplied.Length
                && CryptographicOperations.FixedTimeEquals(expected, supplied);
        }
    }

    private sealed record SignedWebhookRequest(string Timestamp, string Signature, string RawBody);
    private sealed record ShipmentProjection(string Status, DateTimeOffset ChangedAtUtc);

    private enum WebhookReceiveResult
    {
        Applied,
        Duplicate,
        OutOfOrder,
        Unauthorized
    }
}
