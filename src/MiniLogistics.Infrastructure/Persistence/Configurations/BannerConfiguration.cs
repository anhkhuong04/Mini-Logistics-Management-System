using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Banners;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("Banners");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.ImageUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(b => b.LinkUrl)
            .HasMaxLength(1000);

        builder.Property(b => b.IsActive)
            .IsRequired();

        builder.Property(b => b.SortOrder)
            .IsRequired();
    }
}
