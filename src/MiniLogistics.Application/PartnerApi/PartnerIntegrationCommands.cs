using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public sealed record CreatePartnerApiClientCommand(
    Guid CurrentUserId,
    Guid ShopId,
    string Name,
    PartnerApiScope Scopes = PartnerApiScope.All,
    IReadOnlyList<string>? AllowedIpAddresses = null,
    DateTimeOffset? ExpiresAtUtc = null);

public sealed record RotatePartnerApiClientKeyCommand(
    Guid CurrentUserId,
    Guid ApiClientId);

public sealed record SetPartnerApiClientActiveStatusCommand(
    Guid CurrentUserId,
    Guid ApiClientId,
    bool IsActive);

public sealed record UpdatePartnerApiClientSecurityCommand(
    Guid CurrentUserId,
    Guid ApiClientId,
    PartnerApiScope Scopes,
    IReadOnlyList<string> AllowedIpAddresses,
    DateTimeOffset? ExpiresAtUtc);

public sealed record UpsertPartnerWebhookEndpointCommand(
    Guid CurrentUserId,
    Guid ApiClientId,
    string Url,
    string SigningSecret);

public sealed record TestPartnerWebhookCommand(
    Guid CurrentUserId,
    Guid ApiClientId);

public sealed record RetryPartnerWebhookDeliveryCommand(
    Guid CurrentUserId,
    Guid WebhookDeliveryId);
