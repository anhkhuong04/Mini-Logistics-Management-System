using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShipmentImportBatchConfiguration : IEntityTypeConfiguration<ShipmentImportBatch>
{
    public void Configure(EntityTypeBuilder<ShipmentImportBatch> builder)
    {
        builder.ToTable("ShipmentImportBatches");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Id).ValueGeneratedNever();
        builder.Property(batch => batch.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(batch => batch.CreatedAtUtc).IsRequired();

        builder.HasIndex(batch => new { batch.ShopId, batch.CreatedAtUtc });
        builder.HasIndex(batch => new { batch.Status, batch.CreatedAtUtc });

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(batch => batch.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(batch => batch.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(batch => batch.Rows)
            .WithOne()
            .HasForeignKey(row => row.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
