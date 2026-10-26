using System.Text.Json;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;
namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserIntegrationSecurityRequest(IReadOnlyList<string> Scopes, IReadOnlyList<string> AllowedIpAddresses, DateTimeOffset? ExpiresAtUtc);
public sealed record BrowserIntegrationCreateRequest(Guid ShopId, string Name, IReadOnlyList<string> Scopes, IReadOnlyList<string> AllowedIpAddresses, DateTimeOffset? ExpiresAtUtc);
public sealed record BrowserIntegrationActiveRequest(bool? IsActive);
public sealed record BrowserIntegrationWebhookRequest(string Url, string SigningSecret);
public sealed record BrowserIntegrationDashboardResponse(IReadOnlyList<PartnerIntegrationShopResponse> Shops, IReadOnlyList<BrowserIntegrationClientResponse> ApiClients,
    bool GranularPermissionEnabled, IReadOnlyList<PartnerOutboxFailureResponse> OutboxFailures, string Environment);
public sealed record BrowserIntegrationClientResponse(Guid ApiClientId, Guid ShopId, string ShopName, string Name, string ApiKeyPrefix, bool IsActive,
    DateTimeOffset? LastUsedAtUtc, DateTimeOffset CreatedAtUtc, PartnerWebhookEndpointResponse? WebhookEndpoint,
    IReadOnlyList<PartnerWebhookDeliveryResponse> RecentDeliveries, PartnerWebhookMetricsResponse WebhookMetrics,
    IReadOnlyList<PartnerApiCredentialAuditResponse> RecentCredentialAudits, IReadOnlyList<string> Scopes, IReadOnlyList<string> AllowedIpAddresses,
    DateTimeOffset? ExpiresAtUtc, PartnerApiUsageMetricsResponse? Usage);
internal static class BrowserIntegrationContracts
{
    internal static readonly string[] ScopeNames = ["Quote", "CreateShipment", "TrackShipment", "CancelShipment", "WebhookManage"];
    internal static PartnerApiScope Scopes(IReadOnlyList<string>? names) {
        if(names is null || names.Count is <1 or >5 || names.Distinct(StringComparer.Ordinal).Count()!=names.Count) throw new JsonException();
        var flags=PartnerApiScope.None;
        foreach(var name in names) { if(!ScopeNames.Contains(name,StringComparer.Ordinal))throw new JsonException(); flags|=Enum.Parse<PartnerApiScope>(name); }
        return flags;
    }
    internal static void Ips(IReadOnlyList<string>? ips) { if(ips is null || ips.Count>100 || ips.Any(ip=>ip is null||ip.Length>64)||string.Join(',',ips).Length>2000)throw new JsonException(); }
    internal static BrowserIntegrationDashboardResponse Response(PartnerIntegrationDashboardResponse data, Guid? shopId, string prefix) {
        var clients=data.ApiClients.Where(c=>shopId is null||c.ShopId==shopId).Select(c=>new BrowserIntegrationClientResponse(c.ApiClientId,c.ShopId,c.ShopName,c.Name,c.ApiKeyPrefix,c.IsActive,c.LastUsedAtUtc,c.CreatedAtUtc,c.WebhookEndpoint,
            c.RecentDeliveries.Select(d=>d with { LastError=string.IsNullOrEmpty(d.LastError)?null:"Delivery failed. Review correlated server logs." }).ToArray(),c.WebhookMetrics,c.RecentCredentialAudits,
            ScopeNames.Where(name=>c.Scopes.HasFlag(Enum.Parse<PartnerApiScope>(name))).ToArray(),c.AllowedIpAddresses??[],c.ExpiresAtUtc,
            c.Usage is null?null:c.Usage with { LatestFailedRequests=c.Usage.LatestFailedRequests.Select(f=>f with { Path=f.Path.Split('?')[0],ErrorMessage=string.IsNullOrEmpty(f.ErrorMessage)?null:"Request failed." }).ToArray() })).ToArray();
        var ids=clients.Select(c=>c.ApiClientId).ToHashSet();
        return new(data.Shops,clients,data.GranularPermissionEnabled,(data.OutboxFailures??[]).Where(f=>ids.Contains(f.ApiClientId)).ToArray(),prefix=="ml_test_"?"Sandbox":"Live");
    }
}
