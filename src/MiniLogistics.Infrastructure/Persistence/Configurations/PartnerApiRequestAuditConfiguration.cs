using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class PartnerApiRequestAuditConfiguration : IEntityTypeConfiguration<PartnerApiRequestAudit>
{
    public void Configure(EntityTypeBuilder<PartnerApiRequestAudit> builder)
    {
        builder.ToTable("PartnerApiRequestAudits");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id)
            .ValueGeneratedNever();

        builder.Property(audit => audit.Method)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(audit => audit.Path)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(audit => audit.TraceId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(audit => audit.ExternalOrderId)
            .HasMaxLength(100);

        builder.Property(audit => audit.IdempotencyKey)
            .HasMaxLength(150);

        builder.Property(audit => audit.RequestHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(audit => audit.StatusCode)
            .IsRequired();

        builder.Property(audit => audit.DurationMs)
            .IsRequired();

        builder.Property(audit => audit.TrackingCode)
            .HasMaxLength(50);

        builder.Property(audit => audit.ErrorCode)
            .HasMaxLength(100);

        builder.Property(audit => audit.ErrorMessage)
            .HasMaxLength(500);

        builder.Property(audit => audit.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<ApiClient>()
            .WithMany()
            .HasForeignKey(audit => audit.ApiClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(audit => audit.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(audit => new { audit.ApiClientId, audit.CreatedAtUtc });
        builder.HasIndex(audit => audit.TraceId);
        builder.HasIndex(audit => audit.ShipmentId);
    }
}
