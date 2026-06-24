using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class CodTransactionConfiguration : IEntityTypeConfiguration<CodTransaction>
{
    public void Configure(EntityTypeBuilder<CodTransaction> builder)
    {
        builder.ToTable("CodTransactions");

        builder.HasKey(codTransaction => codTransaction.Id);

        builder.Property(codTransaction => codTransaction.Id)
            .ValueGeneratedNever();

        builder.Property(codTransaction => codTransaction.ShipmentId)
            .IsRequired();

        builder.Property(codTransaction => codTransaction.Amount)
            .HasConversion(
                money => money.Amount,
                value => new Money(value))
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(codTransaction => codTransaction.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(codTransaction => codTransaction.CollectedAtUtc);
        builder.Property(codTransaction => codTransaction.CollectedByUserId);
        builder.Property(codTransaction => codTransaction.SettledAtUtc);
        builder.Property(codTransaction => codTransaction.SettledByUserId);

        builder.Property(codTransaction => codTransaction.CreatedAtUtc)
            .IsRequired();

        builder.Property(codTransaction => codTransaction.UpdatedAtUtc);

        builder.HasOne<Shipment>()
            .WithOne()
            .HasForeignKey<CodTransaction>(codTransaction => codTransaction.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(codTransaction => codTransaction.ShipmentId)
            .IsUnique();

        builder.HasIndex(codTransaction => codTransaction.Status);
        builder.HasIndex(codTransaction => codTransaction.CollectedAtUtc);
    }
}
