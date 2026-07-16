using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.AdminAuditing;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("AdminAuditLogs");

        builder.HasKey(auditLog => auditLog.Id);

        builder.Property(auditLog => auditLog.Id)
            .ValueGeneratedNever();

        builder.Property(auditLog => auditLog.ActorUserId)
            .IsRequired();

        builder.Property(auditLog => auditLog.ActorRole)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(auditLog => auditLog.Action)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(auditLog => auditLog.TargetType)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(auditLog => auditLog.TargetId)
            .IsRequired();

        builder.Property(auditLog => auditLog.OldValueJson)
            .HasMaxLength(4000);

        builder.Property(auditLog => auditLog.NewValueJson)
            .HasMaxLength(4000);

        builder.Property(auditLog => auditLog.Reason)
            .HasMaxLength(500);

        builder.Property(auditLog => auditLog.IpAddress)
            .HasMaxLength(64);

        builder.Property(auditLog => auditLog.UserAgent)
            .HasMaxLength(300);

        builder.Property(auditLog => auditLog.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(auditLog => new { auditLog.ActorUserId, auditLog.CreatedAtUtc });
        builder.HasIndex(auditLog => new { auditLog.Action, auditLog.CreatedAtUtc });
        builder.HasIndex(auditLog => new { auditLog.TargetType, auditLog.TargetId, auditLog.CreatedAtUtc });
        builder.HasIndex(auditLog => auditLog.CreatedAtUtc);
    }
}
