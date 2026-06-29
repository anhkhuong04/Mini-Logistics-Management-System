using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");

        builder.HasKey(delivery => delivery.Id);

        builder.Property(delivery => delivery.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(delivery => delivery.PayloadJson)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(delivery => delivery.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(delivery => delivery.LastError)
            .HasMaxLength(1000);

        builder.Property(delivery => delivery.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAtUtc });

        builder.HasIndex(delivery => delivery.AggregateId);

        builder.HasOne<WebhookEndpoint>()
            .WithMany()
            .HasForeignKey(delivery => delivery.WebhookEndpointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApiClient>()
            .WithMany()
            .HasForeignKey(delivery => delivery.ApiClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
