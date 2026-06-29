using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Infrastructure.Persistence.Configurations;

public sealed class ApiClientConfiguration : IEntityTypeConfiguration<ApiClient>
{
    public void Configure(EntityTypeBuilder<ApiClient> builder)
    {
        builder.ToTable("ApiClients");

        builder.HasKey(apiClient => apiClient.Id);

        builder.Property(apiClient => apiClient.Id)
            .ValueGeneratedNever();

        builder.Property(apiClient => apiClient.ShopId)
            .IsRequired();

        builder.Property(apiClient => apiClient.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(apiClient => apiClient.ApiKeyPrefix)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(apiClient => apiClient.ApiKeyHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(apiClient => apiClient.IsActive)
            .IsRequired();

        builder.Property(apiClient => apiClient.LastUsedAtUtc);

        builder.Property(apiClient => apiClient.CreatedAtUtc)
            .IsRequired();

        builder.Property(apiClient => apiClient.UpdatedAtUtc);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(apiClient => apiClient.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(apiClient => apiClient.ApiKeyHash)
            .IsUnique();

        builder.HasIndex(apiClient => new { apiClient.ShopId, apiClient.IsActive });
    }
}
