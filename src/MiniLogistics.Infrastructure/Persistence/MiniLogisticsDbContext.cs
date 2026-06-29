using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Fees;
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

    public DbSet<CodTransaction> CodTransactions => Set<CodTransaction>();

    public DbSet<FeeRule> FeeRules => Set<FeeRule>();

    public DbSet<ApiClient> ApiClients => Set<ApiClient>();

    public DbSet<ExternalShipmentReference> ExternalShipmentReferences => Set<ExternalShipmentReference>();

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

            builder.Property(user => user.CreatedAtUtc)
                .IsRequired();
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiniLogisticsDbContext).Assembly);
    }
}
