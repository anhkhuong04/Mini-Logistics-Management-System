using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Domain.AdminAuditing;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class MiniLogisticsDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public MiniLogisticsDbContext(DbContextOptions<MiniLogisticsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Shop> Shops => Set<Shop>();

    public DbSet<Shipment> Shipments => Set<Shipment>();

    public DbSet<ShipmentAssignment> ShipmentAssignments => Set<ShipmentAssignment>();

    public DbSet<ShipmentStatusHistory> ShipmentStatusHistories => Set<ShipmentStatusHistory>();

    public DbSet<DeliveryProof> DeliveryProofs => Set<DeliveryProof>();

    public DbSet<CodTransaction> CodTransactions => Set<CodTransaction>();

    public DbSet<FeeRule> FeeRules => Set<FeeRule>();

    public DbSet<RouteRegionConfig> RouteRegionConfigs => Set<RouteRegionConfig>();

    public DbSet<Hub> Hubs => Set<Hub>();

    public DbSet<ShipperWorkingArea> ShipperWorkingAreas => Set<ShipperWorkingArea>();

    public DbSet<ApiClient> ApiClients => Set<ApiClient>();

    public DbSet<ExternalShipmentReference> ExternalShipmentReferences => Set<ExternalShipmentReference>();

    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();

    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    public DbSet<PartnerApiRequestAudit> PartnerApiRequestAudits => Set<PartnerApiRequestAudit>();

    public DbSet<PartnerApiCredentialAudit> PartnerApiCredentialAudits => Set<PartnerApiCredentialAudit>();

    public DbSet<IntegrationManagementScope> IntegrationManagementScopes => Set<IntegrationManagementScope>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(builder =>
        {
            builder.Property(user => user.FullName)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(user => user.IsActive)
                .IsRequired();

            builder.Property(user => user.IsAvailableForAssignment)
                .HasDefaultValue(true)
                .IsRequired();

            builder.Property(user => user.MaxActiveShipments)
                .HasDefaultValue(30)
                .IsRequired();

            builder.Property(user => user.CreatedAtUtc)
                .IsRequired();
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiniLogisticsDbContext).Assembly);
    }
}
