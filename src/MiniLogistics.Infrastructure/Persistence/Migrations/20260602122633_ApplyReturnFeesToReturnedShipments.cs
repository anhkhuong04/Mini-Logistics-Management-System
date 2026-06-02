using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplyReturnFeesToReturnedShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Shipments
                SET ReturnFee = ROUND((BaseShippingFee + ExtraWeightFee) * 0.5, 2)
                WHERE Status = 'Returned'
                """);

            migrationBuilder.Sql("""
                UPDATE Shipments
                SET
                    TotalShippingFee = BaseShippingFee + ExtraWeightFee + InsuranceFee + ReturnFee,
                    ShippingFee = BaseShippingFee + ExtraWeightFee + InsuranceFee + ReturnFee
                WHERE Status = 'Returned'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Shipments
                SET ReturnFee = 0
                WHERE Status = 'Returned'
                """);

            migrationBuilder.Sql("""
                UPDATE Shipments
                SET
                    TotalShippingFee = BaseShippingFee + ExtraWeightFee + InsuranceFee,
                    ShippingFee = BaseShippingFee + ExtraWeightFee + InsuranceFee
                WHERE Status = 'Returned'
                """);
        }
    }
}
