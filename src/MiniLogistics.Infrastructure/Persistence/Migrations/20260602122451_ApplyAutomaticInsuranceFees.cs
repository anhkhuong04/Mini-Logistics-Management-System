using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplyAutomaticInsuranceFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Shipments
                SET InsuranceFee =
                    CASE
                        WHEN GoodsValue < 1000000 THEN 0
                        WHEN GoodsValue > 20000000 THEN ROUND(20000000 * 0.005, 2)
                        ELSE ROUND(GoodsValue * 0.005, 2)
                    END
                """);

            migrationBuilder.Sql("""
                UPDATE Shipments
                SET
                    TotalShippingFee = BaseShippingFee + ExtraWeightFee + InsuranceFee + ReturnFee,
                    ShippingFee = BaseShippingFee + ExtraWeightFee + InsuranceFee + ReturnFee
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Shipments
                SET
                    TotalShippingFee = BaseShippingFee + ExtraWeightFee + ReturnFee,
                    ShippingFee = BaseShippingFee + ExtraWeightFee + ReturnFee,
                    InsuranceFee = 0
                """);

        }
    }
}
