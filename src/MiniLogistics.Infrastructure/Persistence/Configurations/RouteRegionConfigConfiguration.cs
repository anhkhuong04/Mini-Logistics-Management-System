using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class RouteRegionConfigConfiguration : IEntityTypeConfiguration<RouteRegionConfig>
{
    public void Configure(EntityTypeBuilder<RouteRegionConfig> builder)
    {
        builder.ToTable("RouteRegionConfigs");

        builder.HasKey(config => config.Id);

        builder.Property(config => config.Id)
            .ValueGeneratedNever();

        builder.Property(config => config.Province)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(config => config.Region)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(config => config.Version)
            .IsRequired();

        builder.Property(config => config.IsActive)
            .IsRequired();

        builder.Property(config => config.CreatedAtUtc)
            .IsRequired();

        builder.Property(config => config.UpdatedAtUtc);

        builder.HasIndex(config => new { config.Province, config.IsActive });
        builder.HasIndex(config => new { config.Province, config.Version });
    }
}
