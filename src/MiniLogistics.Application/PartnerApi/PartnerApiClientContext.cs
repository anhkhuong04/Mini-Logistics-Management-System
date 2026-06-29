namespace MiniLogistics.Application.PartnerApi;

public sealed record PartnerApiClientContext(
    Guid ApiClientId,
    Guid ShopId,
    string Name);
