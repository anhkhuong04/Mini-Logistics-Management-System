using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Documents.Labels;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests.Documents;

public sealed class PdfShipmentLabelRendererTests
{
    [Fact]
    public void RenderPreservesLabelFieldsBarcodeAndValidPdfObjectOffsetsWithoutChangingShipment()
    {
        var shipment = Shipment.Create(
            Guid.NewGuid(), "Sender", new PhoneNumber("0900000000"),
            "Receiver", new PhoneNumber("0911111111"),
            new Address("1 Pickup Street", "Ward A", "Province A"),
            new Address("9 Delivery Street", "Ward B", "Province B"),
            new Weight(1m), new ParcelDimensions(10m, 10m, 10m), new Weight(1m),
            new Money(500000m), new Money(100000m),
            new ShippingFeeBreakdown(new Money(20000m), Money.Zero, Money.Zero, Money.Zero),
            RouteType.InterRegion, Guid.NewGuid(), DateTimeOffset.Parse("2026-10-01T00:00:00Z", CultureInfo.InvariantCulture),
            "Handle with care", new TrackingCode("ML-LABEL-001"));
        var historyCount = shipment.StatusHistory.Count;

        var pdf = new PdfShipmentLabelRenderer().Render(shipment);
        var text = Encoding.ASCII.GetString(pdf);

        Assert.StartsWith("%PDF-1.4\n", text);
        Assert.EndsWith("%%EOF", text);
        foreach (var value in new[] { "ML-LABEL-001", "CODE39: *ML-LABEL-001*", "Receiver", "0911111111", "Sender", "Handle with care", "100,000 VND" })
            Assert.Contains(value, text);
        var xref = Regex.Match(text, @"startxref\n(\d+)\n");
        Assert.True(xref.Success);
        Assert.StartsWith("xref\n", text[int.Parse(xref.Groups[1].Value, CultureInfo.InvariantCulture)..]);
        var entries = Regex.Matches(text, @"(?m)^(\d{10}) 00000 n \n");
        Assert.Equal(6, entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var offset = int.Parse(entries[index].Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.StartsWith($"{index + 1} 0 obj\n", text[offset..]);
        }
        Assert.Equal(ShipmentStatus.PendingPickup, shipment.Status);
        Assert.Equal(historyCount, shipment.StatusHistory.Count);
    }
}
