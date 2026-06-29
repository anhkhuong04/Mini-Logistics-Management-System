namespace MiniLogistics.Application.PartnerApi;

public sealed record CreatePartnerApiClientCommand(
    Guid CurrentUserId,
    Guid ShopId,
    string Name);

public sealed record RotatePartnerApiClientKeyCommand(
    Guid CurrentUserId,
    Guid ApiClientId);

public sealed record SetPartnerApiClientActiveStatusCommand(
    Guid CurrentUserId,
    Guid ApiClientId,
    bool IsActive);

public sealed record UpsertPartnerWebhookEndpointCommand(
    Guid CurrentUserId,
    Guid ApiClientId,
    string Url,
    string SigningSecret);

public sealed record TestPartnerWebhookCommand(
    Guid CurrentUserId,
    Guid ApiClientId);
