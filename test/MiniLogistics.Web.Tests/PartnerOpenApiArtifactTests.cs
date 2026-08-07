using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class PartnerOpenApiArtifactTests
{
    [Fact]
    public void GeneratedDocument_ContainsVersionedPartnerContractAndSecurityMetadata()
    {
        var repositoryRoot = FindRepositoryRoot();
        var documentPath = Path.Combine(repositoryRoot, "docs", "partner-api.openapi.json");
        using var document = JsonDocument.Parse(File.ReadAllText(documentPath));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        Assert.Equal("MiniLogistics Partner API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal(4, paths.EnumerateObject().Count());
        Assert.True(paths.TryGetProperty("/api/v1/partner/shipping/quote", out _));
        Assert.True(paths.TryGetProperty("/api/v1/partner/shipments", out var shipments));
        Assert.True(paths.TryGetProperty("/api/v1/partner/shipments/{trackingCode}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/partner/shipments/{trackingCode}/cancel", out _));

        var createParameters = shipments.GetProperty("post").GetProperty("parameters");
        Assert.Contains(
            createParameters.EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.True(root.GetProperty("components")
            .GetProperty("securitySchemes")
            .TryGetProperty("BearerApiKey", out _));
        Assert.Contains(
            "30 requests/minute",
            shipments.GetProperty("post").GetProperty("description").GetString(),
            StringComparison.Ordinal);

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var trackingProperties = schemas
            .GetProperty("PartnerShipmentTrackingResponse")
            .GetProperty("properties");
        Assert.True(trackingProperties.TryGetProperty("externalOrderId", out var externalOrderId));
        Assert.Contains(
            externalOrderId.GetProperty("type").EnumerateArray(),
            type => type.GetString() == "null");

        var timelineProperties = schemas
            .GetProperty("PartnerShipmentTimelineItem")
            .GetProperty("properties");
        Assert.True(timelineProperties.TryGetProperty("messageCode", out _));
        Assert.True(timelineProperties.TryGetProperty("message", out _));
        Assert.True(timelineProperties.TryGetProperty("locale", out _));
        Assert.False(timelineProperties.TryGetProperty("note", out _));
    }

    [Fact]
    public void PostmanCollection_MatchesEveryOpenApiMethodAndPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var openApi = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repositoryRoot, "docs", "partner-api.openapi.json")));
        using var postman = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repositoryRoot, "postman", "partner-api.postman_collection.json")));

        var openApiOperations = openApi.RootElement.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation =>
                $"{operation.Name.ToUpperInvariant()} {path.Name}"))
            .ToHashSet(StringComparer.Ordinal);
        var postmanOperations = postman.RootElement.GetProperty("item")
            .EnumerateArray()
            .Select(item => item.GetProperty("request"))
            .Select(request =>
            {
                var method = request.GetProperty("method").GetString();
                var url = request.GetProperty("url").GetString()!
                    .Replace("{{baseUrl}}", string.Empty, StringComparison.Ordinal)
                    .Replace("{{trackingCode}}", "{trackingCode}", StringComparison.Ordinal);
                return $"{method} {url}";
            })
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(openApiOperations, postmanOperations);
    }

    [Fact]
    public void PartnerDocumentation_RelativeLinksResolveToRepositoryFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var documents = new[]
        {
            "docs/partner-api.md",
            "docs/third-party-shipment-integration-guide.md",
            "docs/partner-api-changelog.md",
            "docs/partner-api-production-readiness-report.md",
            "docs/architecture/adr-001-partner-api-production-contract.md",
            "docs/security/partner-api-threat-model.md",
            "docs/operations/partner-api-production-runbook.md"
        };

        foreach (var relativeDocument in documents)
        {
            var documentPath = Path.Combine(repositoryRoot, relativeDocument);
            var directory = Path.GetDirectoryName(documentPath)!;
            foreach (Match match in Regex.Matches(File.ReadAllText(documentPath), @"\[[^\]]+\]\(([^)#]+)"))
            {
                var target = Uri.UnescapeDataString(match.Groups[1].Value);
                if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Assert.True(
                    File.Exists(Path.GetFullPath(Path.Combine(directory, target))),
                    $"Broken relative link '{target}' in '{relativeDocument}'.");
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "task.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root containing task.md was not found.");
    }
}
