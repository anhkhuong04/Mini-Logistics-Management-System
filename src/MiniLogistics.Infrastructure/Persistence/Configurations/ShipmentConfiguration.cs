using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");

        builder.HasKey(shipment => shipment.Id);

        builder.Property(shipment => shipment.Id)
            .ValueGeneratedNever();

        builder.Property(shipment => shipment.ShopId)
            .IsRequired();

        builder.Property(shipment => shipment.TrackingCode)
            .HasConversion(
                trackingCode => trackingCode.Value,
                value => new TrackingCode(value))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(shipment => shipment.SenderName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(shipment => shipment.SenderPhone)
            .HasConversion(
                phoneNumber => phoneNumber.Value,
                value => new PhoneNumber(value))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(shipment => shipment.ReceiverName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(shipment => shipment.ReceiverPhone)
            .HasConversion(
                phoneNumber => phoneNumber.Value,
                value => new PhoneNumber(value))
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(shipment => shipment.PickupAddress, addressBuilder =>
        {
            addressBuilder.Property(address => address.Street)
                .HasColumnName("PickupAddressLine")
                .HasMaxLength(300)
                .IsRequired();

            addressBuilder.Property(address => address.Ward)
                .HasColumnName("PickupWard")
                .HasMaxLength(100)
                .IsRequired();

            addressBuilder.Property(address => address.Province)
                .HasColumnName("PickupProvince")
                .HasMaxLength(100)
                .IsRequired();

            addressBuilder.Property(address => address.Country)
                .HasColumnName("PickupCountry")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.OwnsOne(shipment => shipment.DeliveryAddress, addressBuilder =>
        {
            addressBuilder.Property(address => address.Street)
                .HasColumnName("DeliveryAddressLine")
                .HasMaxLength(300)
                .IsRequired();

            addressBuilder.Property(address => address.Ward)
                .HasColumnName("DeliveryWard")
                .HasMaxLength(100)
                .IsRequired();

            addressBuilder.Property(address => address.Province)
                .HasColumnName("DeliveryProvince")
                .HasMaxLength(100)
                .IsRequired();

            addressBuilder.Property(address => address.Country)
                .HasColumnName("DeliveryCountry")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.Property(shipment => shipment.Weight)
            .HasConversion(
                weight => weight.Kilograms,
                value => new Weight(value))
            .HasColumnName("WeightInKg")
            .HasPrecision(10, 3)
            .IsRequired();

        builder.OwnsOne(shipment => shipment.ParcelDimensions, dimensionsBuilder =>
        {
            dimensionsBuilder.Property(dimensions => dimensions.LengthCm)
                .HasColumnName("ParcelLengthCm")
                .HasPrecision(10, 2)
                .IsRequired();

            dimensionsBuilder.Property(dimensions => dimensions.WidthCm)
                .HasColumnName("ParcelWidthCm")
                .HasPrecision(10, 2)
                .IsRequired();

            dimensionsBuilder.Property(dimensions => dimensions.HeightCm)
                .HasColumnName("ParcelHeightCm")
                .HasPrecision(10, 2)
                .IsRequired();
        });

        builder.Property(shipment => shipment.ChargeableWeight)
            .HasConversion(
                weight => weight.Kilograms,
                value => new Weight(value))
            .HasColumnName("ChargeableWeightInKg")
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(shipment => shipment.GoodsValue)
            .HasConversion(
                money => money.Amount,
                value => new Money(value))
            .HasColumnName("GoodsValue")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(shipment => shipment.CodAmount)
            .HasConversion(
                money => money.Amount,
                value => new Money(value))
            .HasColumnName("CodAmount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(shipment => shipment.ShippingFee)
            .HasConversion(
                money => money.Amount,
                value => new Money(value))
            .HasColumnName("ShippingFee")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.OwnsOne(shipment => shipment.ShippingFeeBreakdown, breakdownBuilder =>
        {
            breakdownBuilder.Property(breakdown => breakdown.BaseFee)
                .HasConversion(
                    money => money.Amount,
                    value => new Money(value))
                .HasColumnName("BaseShippingFee")
                .HasPrecision(18, 2)
                .IsRequired();

            breakdownBuilder.Property(breakdown => breakdown.ExtraWeightFee)
                .HasConversion(
                    money => money.Amount,
                    value => new Money(value))
                .HasColumnName("ExtraWeightFee")
                .HasPrecision(18, 2)
                .IsRequired();

            breakdownBuilder.Property(breakdown => breakdown.InsuranceFee)
                .HasConversion(
                    money => money.Amount,
                    value => new Money(value))
                .HasColumnName("InsuranceFee")
                .HasPrecision(18, 2)
                .IsRequired();

            breakdownBuilder.Property(breakdown => breakdown.ReturnFee)
                .HasConversion(
                    money => money.Amount,
                    value => new Money(value))
                .HasColumnName("ReturnFee")
                .HasPrecision(18, 2)
                .IsRequired();

            breakdownBuilder.Property(breakdown => breakdown.TotalFee)
                .HasConversion(
                    money => money.Amount,
                    value => new Money(value))
                .HasColumnName("TotalShippingFee")
                .HasPrecision(18, 2)
                .IsRequired();
        });

        builder.Property(shipment => shipment.RouteType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(shipment => shipment.Note)
            .HasMaxLength(500);

        builder.Property(shipment => shipment.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(shipment => shipment.CreatedAtUtc)
            .IsRequired();

        builder.Property(shipment => shipment.UpdatedAtUtc);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(shipment => shipment.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(shipment => shipment.Assignments)
            .WithOne()
            .HasForeignKey(assignment => assignment.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(shipment => shipment.Assignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(shipment => shipment.StatusHistory)
            .WithOne()
            .HasForeignKey(history => history.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(shipment => shipment.StatusHistory)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(shipment => shipment.TrackingCode)
            .IsUnique();

        builder.HasIndex(shipment => shipment.ShopId);
        builder.HasIndex(shipment => shipment.Status);
        builder.HasIndex(shipment => shipment.CreatedAtUtc);
        builder.HasIndex(shipment => shipment.ReceiverPhone);
    }
}
