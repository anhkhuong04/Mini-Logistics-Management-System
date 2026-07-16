using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniLogistics.Infrastructure.Persistence;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniLogisticsDbContext))]
    [Migration("20260716090000_AddAdminAuditLogs")]
    partial class AddAdminAuditLogs
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "10.0.0");
#pragma warning restore 612, 618
        }
    }
}
