using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class HubConfiguration : IEntityTypeConfiguration<Hub>
{
    public void Configure(EntityTypeBuilder<Hub> builder)
    {
        builder.ToTable("Hubs");

        builder.HasKey(hub => hub.Id);

        builder.Property(hub => hub.Id)
            .ValueGeneratedNever();

        builder.Property(hub => hub.Code)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(hub => hub.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(hub => hub.Province)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(hub => hub.Ward)
            .HasMaxLength(100);

        builder.Property(hub => hub.AddressLine)
            .HasMaxLength(300);

        builder.Property(hub => hub.Country)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(hub => hub.IsRegionalSortingHub)
            .IsRequired();

        builder.Property(hub => hub.IsActive)
            .IsRequired();

        builder.Property(hub => hub.CreatedAtUtc)
            .IsRequired();

        builder.Property(hub => hub.UpdatedAtUtc);

        builder.HasIndex(hub => hub.Code)
            .IsUnique();

        builder.HasIndex(hub => hub.Province);
        builder.HasIndex(hub => hub.IsActive);
    }
}
