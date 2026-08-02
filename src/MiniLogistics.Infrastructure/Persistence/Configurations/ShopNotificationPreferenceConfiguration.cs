using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShopNotificationPreferenceConfiguration : IEntityTypeConfiguration<ShopNotificationPreference>
{
    public void Configure(EntityTypeBuilder<ShopNotificationPreference> builder)
    {
        builder.ToTable("ShopNotificationPreferences");
        builder.HasKey(preference => preference.Id);
        builder.Property(preference => preference.Id).ValueGeneratedNever();
        builder.Property(preference => preference.EnabledEventTypes).HasMaxLength(1000).IsRequired();
        builder.Property(preference => preference.CreatedAtUtc).IsRequired();
        builder.HasIndex(preference => new { preference.ShopId, preference.UserId }).IsUnique();

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(preference => preference.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
