using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShopStaffMembershipConfiguration : IEntityTypeConfiguration<ShopStaffMembership>
{
    public void Configure(EntityTypeBuilder<ShopStaffMembership> builder)
    {
        builder.ToTable("ShopStaffMemberships");
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).ValueGeneratedNever();
        builder.Property(membership => membership.Role).HasConversion<int>().IsRequired();
        builder.Property(membership => membership.Permissions).HasConversion<int>().IsRequired();
        builder.Property(membership => membership.IsActive).IsRequired();
        builder.Property(membership => membership.CreatedAtUtc).IsRequired();
        builder.HasIndex(membership => new { membership.ShopId, membership.UserId }).IsUnique();
        builder.HasIndex(membership => new { membership.UserId, membership.IsActive });

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(membership => membership.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.CreatedByOwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
