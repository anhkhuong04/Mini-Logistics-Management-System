using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class PartnerApiCredentialAuditConfiguration : IEntityTypeConfiguration<PartnerApiCredentialAudit>
{
    public void Configure(EntityTypeBuilder<PartnerApiCredentialAudit> builder)
    {
        builder.ToTable("PartnerApiCredentialAudits");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id)
            .ValueGeneratedNever();

        builder.Property(audit => audit.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(audit => audit.IsSuccess)
            .IsRequired();

        builder.Property(audit => audit.TraceId)
            .HasMaxLength(100);

        builder.Property(audit => audit.IpHash)
            .HasMaxLength(128);

        builder.Property(audit => audit.UserAgent)
            .HasMaxLength(300);

        builder.Property(audit => audit.ErrorCode)
            .HasMaxLength(100);

        builder.Property(audit => audit.ErrorMessage)
            .HasMaxLength(500);

        builder.Property(audit => audit.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(audit => audit.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApiClient>()
            .WithMany()
            .HasForeignKey(audit => audit.ApiClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(audit => new { audit.ApiClientId, audit.CreatedAtUtc });
        builder.HasIndex(audit => new { audit.ShopId, audit.CreatedAtUtc });
        builder.HasIndex(audit => new { audit.ActorUserId, audit.CreatedAtUtc });
        builder.HasIndex(audit => audit.Action);
    }
}
