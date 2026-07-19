using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Domain.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void Money_RejectsNegativeAmountsAndCrossCurrencyArithmetic()
    {
        Assert.Throws<DomainException>(() => new Money(-0.01m));

        var vnd = new Money(10_000m, "VND");
        var usd = new Money(1m, "USD");

        Assert.Throws<DomainException>(() => _ = vnd + usd);
        Assert.Throws<DomainException>(() => _ = vnd * -1m);
    }

    [Fact]
    public void Weight_RejectsZeroOrNegativeValues()
    {
        Assert.Throws<DomainException>(() => new Weight(0m));
        Assert.Throws<DomainException>(() => new Weight(-1m));
        Assert.Equal(1.235m, new Weight(1.23456m).Kilograms);
    }

    [Fact]
    public void PhoneNumber_NormalizesSpacesAndRejectsInvalidInput()
    {
        var phoneNumber = new PhoneNumber(" 090 000 0000 ");

        Assert.Equal("0900000000", phoneNumber.Value);
        Assert.Throws<DomainException>(() => new PhoneNumber("1234"));
        Assert.Throws<DomainException>(() => new PhoneNumber("not-a-phone"));
    }

    [Fact]
    public void Address_RequiresLocationParts()
    {
        var address = new Address("1 Nguyen Trai", "Ben Thanh", "Ho Chi Minh");

        Assert.Equal("1 Nguyen Trai, Ben Thanh, Ho Chi Minh, Vietnam", address.FullAddress);
        Assert.Throws<DomainException>(() => new Address("1 Nguyen Trai", "", "Ho Chi Minh"));
    }

    [Fact]
    public void ParcelDimensions_CalculateVolumetricWeightAndRejectInvalidValues()
    {
        var dimensions = new ParcelDimensions(20m, 10m, 10m);

        Assert.Equal(0.4m, dimensions.CalculateVolumetricWeightKg());
        Assert.Throws<DomainException>(() => new ParcelDimensions(0m, 10m, 10m));
        Assert.Throws<DomainException>(() => dimensions.CalculateVolumetricWeightKg(0m));
    }
}
