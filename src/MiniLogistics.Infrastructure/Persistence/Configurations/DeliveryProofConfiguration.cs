using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class DeliveryProofConfiguration : IEntityTypeConfiguration<DeliveryProof>
{
    public void Configure(EntityTypeBuilder<DeliveryProof> builder)
    {
        builder.ToTable("DeliveryProofs");

        builder.HasKey(proof => proof.Id);

        builder.Property(proof => proof.Id)
            .ValueGeneratedNever();

        builder.Property(proof => proof.ShipmentId)
            .IsRequired();

        builder.Property(proof => proof.ProofType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(proof => proof.ProofMethod)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(proof => proof.ResourceUri)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(proof => proof.RecipientName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(proof => proof.VerificationText)
            .HasMaxLength(150);

        builder.Property(proof => proof.Latitude)
            .HasPrecision(9, 6);

        builder.Property(proof => proof.Longitude)
            .HasPrecision(9, 6);

        builder.Property(proof => proof.GpsAccuracyMeters)
            .HasPrecision(10, 2);

        builder.Property(proof => proof.GpsCapturedAtUtc);

        builder.Property(proof => proof.SubmittedByUserId)
            .IsRequired();

        builder.Property(proof => proof.CapturedAtUtc)
            .IsRequired();

        builder.Property(proof => proof.SubmittedAtUtc)
            .IsRequired();

        builder.Property(proof => proof.CreatedAtUtc)
            .IsRequired();

        builder.Property(proof => proof.UpdatedAtUtc);

        builder.HasOne<Shipment>()
            .WithMany()
            .HasForeignKey(proof => proof.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(proof => new { proof.ShipmentId, proof.ProofType, proof.SubmittedAtUtc });
        builder.HasIndex(proof => proof.SubmittedByUserId);
    }
}
