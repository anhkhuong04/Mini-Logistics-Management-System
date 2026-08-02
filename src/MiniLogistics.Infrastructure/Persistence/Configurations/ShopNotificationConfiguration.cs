using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShopNotificationConfiguration : IEntityTypeConfiguration<ShopNotification>
{
    public void Configure(EntityTypeBuilder<ShopNotification> builder)
    {
        builder.ToTable("ShopNotifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();
        builder.Property(notification => notification.EventType).HasMaxLength(80).IsRequired();
        builder.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Message).HasMaxLength(500).IsRequired();
        builder.Property(notification => notification.CreatedAtUtc).IsRequired();
        builder.HasIndex(notification => new { notification.ShopId, notification.UserId, notification.IsRead, notification.CreatedAtUtc });

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(notification => notification.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Shipment>()
            .WithMany()
            .HasForeignKey(notification => notification.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
