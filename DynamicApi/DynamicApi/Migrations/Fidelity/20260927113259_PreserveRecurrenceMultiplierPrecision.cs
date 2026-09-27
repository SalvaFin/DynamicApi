using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class PreserveRecurrenceMultiplierPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "BenefitMultiplierSnapshot",
                table: "fidelity_points_transactions",
                type: "decimal(30,28)",
                precision: 30,
                scale: 28,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,4)",
                oldPrecision: 10,
                oldScale: 4,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "BenefitMultiplierSnapshot",
                table: "fidelity_points_transactions",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(30,28)",
                oldPrecision: 30,
                oldScale: 28,
                oldNullable: true);
        }
    }
}
