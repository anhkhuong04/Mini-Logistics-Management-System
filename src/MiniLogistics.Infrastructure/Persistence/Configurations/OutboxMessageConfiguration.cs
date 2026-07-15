using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.Outbox;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.Type)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(message => message.PayloadJson)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(message => message.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(message => message.LastError)
            .HasMaxLength(1000);

        builder.Property(message => message.CreatedAtUtc)
            .IsRequired();

        builder.Property(message => message.UpdatedAtUtc);

        builder.HasIndex(message => new { message.Status, message.NextAttemptAtUtc });
        builder.HasIndex(message => new { message.Type, message.AggregateId });
        builder.HasIndex(message => message.CreatedAtUtc);
    }
}
