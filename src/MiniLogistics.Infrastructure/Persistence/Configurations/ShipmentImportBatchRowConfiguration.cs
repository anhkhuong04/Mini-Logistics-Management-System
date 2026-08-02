using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShipmentImportBatchRowConfiguration : IEntityTypeConfiguration<ShipmentImportBatchRow>
{
    public void Configure(EntityTypeBuilder<ShipmentImportBatchRow> builder)
    {
        builder.ToTable("ShipmentImportBatchRows");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedNever();
        builder.Property(row => row.ClientOrderCode).HasMaxLength(100);
        builder.Property(row => row.PayloadJson).HasMaxLength(4000).IsRequired();
        builder.Property(row => row.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(row => row.TrackingCode).HasMaxLength(50);
        builder.Property(row => row.Errors).HasMaxLength(4000);
        builder.Property(row => row.CreatedAtUtc).IsRequired();

        builder.HasIndex(row => new { row.BatchId, row.RowNumber }).IsUnique();
        builder.HasIndex(row => new { row.ShopId, row.ClientOrderCode, row.Status });

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(row => row.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Shipment>()
            .WithMany()
            .HasForeignKey(row => row.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
