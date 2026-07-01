using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity;

[DbContext(typeof(DynamicFidelityDbContext))]
[Migration("20260701144500_AllowNullValueForFreeTickets")]
public partial class AllowNullValueForFreeTickets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "Valor",
            table: "fidelity_tickets",
            type: "decimal(10,2)",
            precision: 10,
            scale: 2,
            nullable: true,
            oldClrType: typeof(decimal),
            oldType: "decimal(10,2)",
            oldPrecision: 10,
            oldScale: 2);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE `fidelity_tickets` SET `Valor` = 0 WHERE `Valor` IS NULL;");

        migrationBuilder.AlterColumn<decimal>(
            name: "Valor",
            table: "fidelity_tickets",
            type: "decimal(10,2)",
            precision: 10,
            scale: 2,
            nullable: false,
            defaultValue: 0m,
            oldClrType: typeof(decimal),
            oldType: "decimal(10,2)",
            oldPrecision: 10,
            oldScale: 2,
            oldNullable: true);
    }
}
