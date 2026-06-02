using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShipmentStatusHistoryConfiguration : IEntityTypeConfiguration<ShipmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<ShipmentStatusHistory> builder)
    {
        builder.ToTable("ShipmentStatusHistories");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.ShipmentId)
            .IsRequired();

        builder.Property(history => history.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(history => history.ChangedByUserId)
            .IsRequired();

        builder.Property(history => history.Note)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(history => history.ChangedAtUtc)
            .IsRequired();

        builder.HasIndex(history => new { history.ShipmentId, history.ChangedAtUtc });
        builder.HasIndex(history => history.ChangedByUserId);
    }
}
