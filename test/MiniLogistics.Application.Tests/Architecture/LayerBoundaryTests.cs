using System.Reflection;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.Shipments;
using Xunit;

namespace MiniLogistics.Application.Tests.Architecture;

public sealed class LayerBoundaryTests
{
    [Fact]
    public void InnerLayersRemainIndependentOfInfrastructureWebAndPersistenceFrameworks()
    {
        Assert.DoesNotContain(typeof(Shipment).Assembly.GetReferencedAssemblies(), reference =>
            reference.Name!.StartsWith("MiniLogistics.", StringComparison.Ordinal)
            || IsOuterFramework(reference));
        Assert.DoesNotContain(typeof(IShipmentRepository).Assembly.GetReferencedAssemblies(), reference =>
            reference.Name is "MiniLogistics.Infrastructure" or "MiniLogistics.Web"
            || IsOuterFramework(reference));
    }

    [Fact]
    public void ShipmentReadPortRequiresExplicitImplementationsAndExcludesTrackedMutationLoads()
    {
        var reads = typeof(IShipmentReadRepository).GetMethods();
        Assert.All(reads, method => Assert.True(method.IsAbstract, $"{method.Name} must not supply a fallback query."));
        Assert.DoesNotContain(reads, method => method.Name.StartsWith("GetTracked", StringComparison.Ordinal));
        Assert.Contains(typeof(IShipmentWriteRepository).GetMethods(), method => method.Name == "GetTrackedByIdAsync");
        Assert.Contains(typeof(IShipmentWriteRepository).GetMethods(), method => method.Name == "GetTrackedByIdAndShopIdAsync");
        Assert.Contains(typeof(IShipmentWriteRepository).GetMethods(), method => method.Name == "GetTrackedByTrackingCodeAndShopIdAsync");
    }

    private static bool IsOuterFramework(AssemblyName reference) =>
        reference.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
        || reference.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
        || reference.Name.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal);
}
