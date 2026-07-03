using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShipperWorkingAreaConfiguration : IEntityTypeConfiguration<ShipperWorkingArea>
{
    public void Configure(EntityTypeBuilder<ShipperWorkingArea> builder)
    {
        builder.ToTable("ShipperWorkingAreas");

        builder.HasKey(area => area.Id);

        builder.Property(area => area.Id)
            .ValueGeneratedNever();

        builder.Property(area => area.ShipperId)
            .IsRequired();

        builder.Property(area => area.HubId)
            .IsRequired();

        builder.Property(area => area.Province)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(area => area.Ward)
            .HasMaxLength(100);

        builder.Property(area => area.ZoneCode)
            .HasMaxLength(60);

        builder.Property(area => area.IsActive)
            .IsRequired();

        builder.Property(area => area.CreatedAtUtc)
            .IsRequired();

        builder.Property(area => area.UpdatedAtUtc);

        builder.HasOne<Hub>()
            .WithMany()
            .HasForeignKey(area => area.HubId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(area => area.ShipperId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(area => area.ShipperId);
        builder.HasIndex(area => area.HubId);
        builder.HasIndex(area => area.Province);
        builder.HasIndex(area => area.IsActive);

        builder.HasIndex(area => new { area.ShipperId, area.HubId, area.Ward, area.ZoneCode })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
