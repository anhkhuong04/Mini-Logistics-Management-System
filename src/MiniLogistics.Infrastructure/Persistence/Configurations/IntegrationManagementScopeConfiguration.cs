using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class IntegrationManagementScopeConfiguration : IEntityTypeConfiguration<IntegrationManagementScope>
{
    public void Configure(EntityTypeBuilder<IntegrationManagementScope> builder)
    {
        builder.ToTable("IntegrationManagementScopes");

        builder.HasKey(scope => scope.Id);

        builder.Property(scope => scope.Id)
            .ValueGeneratedNever();

        builder.Property(scope => scope.ActorUserId)
            .IsRequired();

        builder.Property(scope => scope.Province)
            .HasMaxLength(100);

        builder.Property(scope => scope.IsGlobal)
            .IsRequired();

        builder.Property(scope => scope.IsActive)
            .IsRequired();

        builder.Property(scope => scope.CreatedAtUtc)
            .IsRequired();

        builder.Property(scope => scope.UpdatedAtUtc);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(scope => scope.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(scope => new { scope.ActorUserId, scope.IsActive });
        builder.HasIndex(scope => new { scope.Province, scope.IsActive });
        builder.HasIndex(scope => scope.ShopId);
    }
}
