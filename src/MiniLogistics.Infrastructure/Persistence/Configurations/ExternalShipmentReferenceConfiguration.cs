using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ExternalShipmentReferenceConfiguration : IEntityTypeConfiguration<ExternalShipmentReference>
{
    public void Configure(EntityTypeBuilder<ExternalShipmentReference> builder)
    {
        builder.ToTable("ExternalShipmentReferences");

        builder.HasKey(reference => reference.Id);

        builder.Property(reference => reference.Id)
            .ValueGeneratedNever();

        builder.Property(reference => reference.ApiClientId)
            .IsRequired();

        builder.Property(reference => reference.ShopId)
            .IsRequired();

        builder.Property(reference => reference.ShipmentId)
            .IsRequired();

        builder.Property(reference => reference.ExternalOrderId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(reference => reference.IdempotencyKey)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(reference => reference.RequestHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(reference => reference.ResponseSnapshotJson)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(reference => reference.CreatedAtUtc)
            .IsRequired();

        builder.Property(reference => reference.UpdatedAtUtc);

        builder.HasOne<ApiClient>()
            .WithMany()
            .HasForeignKey(reference => reference.ApiClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(reference => reference.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Shipment>()
            .WithMany()
            .HasForeignKey(reference => reference.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(reference => new { reference.ApiClientId, reference.IdempotencyKey })
            .IsUnique();

        builder.HasIndex(reference => new { reference.ApiClientId, reference.ExternalOrderId })
            .IsUnique();

        builder.HasIndex(reference => reference.ShipmentId);
    }
}
