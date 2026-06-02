using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ShipmentAssignmentConfiguration : IEntityTypeConfiguration<ShipmentAssignment>
{
    public void Configure(EntityTypeBuilder<ShipmentAssignment> builder)
    {
        builder.ToTable("ShipmentAssignments");

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.ShipmentId)
            .IsRequired();

        builder.Property(assignment => assignment.ShipperId)
            .IsRequired();

        builder.Property(assignment => assignment.AssignedAtUtc)
            .IsRequired();

        builder.Property(assignment => assignment.UnassignedAtUtc);

        builder.Ignore(assignment => assignment.IsActive);

        builder.HasIndex(assignment => assignment.ShipmentId);
        builder.HasIndex(assignment => assignment.ShipperId);

        builder.HasIndex(assignment => assignment.ShipmentId)
            .IsUnique()
            .HasFilter("[UnassignedAtUtc] IS NULL");
    }
}
