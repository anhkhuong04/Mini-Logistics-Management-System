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

        builder.Property(history => history.Id)
            .ValueGeneratedNever();

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

        builder.Property(history => history.FailureReasonCode)
            .HasConversion<string>()
            .HasMaxLength(80);

        builder.Property(history => history.Latitude)
            .HasPrecision(9, 6);

        builder.Property(history => history.Longitude)
            .HasPrecision(9, 6);

        builder.Property(history => history.GpsAccuracyMeters)
            .HasPrecision(10, 2);

        builder.Property(history => history.GpsCapturedAtUtc);

        builder.HasIndex(history => new { history.ShipmentId, history.ChangedAtUtc });
        builder.HasIndex(history => history.ChangedByUserId);
    }
}
