using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.ToTable("WebhookEndpoints");

        builder.HasKey(endpoint => endpoint.Id);

        builder.Property(endpoint => endpoint.Url)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(endpoint => endpoint.ProtectedSigningSecret)
            .HasColumnName("SigningSecret")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(endpoint => endpoint.IsActive)
            .IsRequired();

        builder.Property(endpoint => endpoint.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(endpoint => new { endpoint.ApiClientId, endpoint.IsActive });

        builder.HasOne<ApiClient>()
            .WithMany()
            .HasForeignKey(endpoint => endpoint.ApiClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
