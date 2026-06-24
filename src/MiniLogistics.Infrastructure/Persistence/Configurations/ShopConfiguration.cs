using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("Shops");

        builder.HasKey(shop => shop.Id);

        builder.Property(shop => shop.Id)
            .ValueGeneratedNever();

        builder.Property(shop => shop.OwnerUserId)
            .IsRequired();

        builder.Property(shop => shop.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(shop => shop.PhoneNumber)
            .HasConversion(
                phoneNumber => phoneNumber.Value,
                value => new PhoneNumber(value))
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(shop => shop.Address, addressBuilder =>
        {
            addressBuilder.Property(address => address.Street)
                .HasColumnName("AddressLine")
                .HasMaxLength(300)
                .IsRequired();

            addressBuilder.Property(address => address.Ward)
                .HasColumnName("Ward")
                .HasMaxLength(100)
                .IsRequired();

            addressBuilder.Property(address => address.Province)
                .HasColumnName("Province")
                .HasMaxLength(100)
                .IsRequired();

            addressBuilder.Property(address => address.Country)
                .HasColumnName("Country")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.Property(shop => shop.IsActive)
            .IsRequired();

        builder.Property(shop => shop.CreatedAtUtc)
            .IsRequired();

        builder.Property(shop => shop.UpdatedAtUtc);

        builder.HasIndex(shop => shop.OwnerUserId);
        builder.HasIndex(shop => shop.PhoneNumber);
    }
}
