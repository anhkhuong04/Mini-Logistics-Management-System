using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MiniLogisticsDbContext>
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=MiniLogisticsDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public MiniLogisticsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MiniLogisticsDbContext>();
        optionsBuilder.UseSqlServer(
            DefaultConnectionString,
            sqlOptions => sqlOptions.MigrationsAssembly(typeof(MiniLogisticsDbContext).Assembly.FullName));

        return new MiniLogisticsDbContext(optionsBuilder.Options);
    }
}
