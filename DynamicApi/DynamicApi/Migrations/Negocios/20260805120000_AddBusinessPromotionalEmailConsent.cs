using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Negocios;

[DbContext(typeof(DynamicNegociosDbContext))]
[Migration("20260805120000_AddBusinessPromotionalEmailConsent")]
public class AddBusinessPromotionalEmailConsent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "CorreosPromocionalesAceptadosAtUtc",
            table: "negocio_audience_memberships",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CorreosPromocionalesRevocadosAtUtc",
            table: "negocio_audience_memberships",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "PermiteCorreosPromocionales",
            table: "negocio_audience_memberships",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: true);

        migrationBuilder.Sql(
            """
            UPDATE negocio_audience_memberships
            SET CorreosPromocionalesAceptadosAtUtc = COALESCE(FechaAltaUtc, CreatedAtUtc, UTC_TIMESTAMP(6))
            WHERE PermiteCorreosPromocionales = TRUE
              AND CorreosPromocionalesAceptadosAtUtc IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CorreosPromocionalesAceptadosAtUtc",
            table: "negocio_audience_memberships");

        migrationBuilder.DropColumn(
            name: "CorreosPromocionalesRevocadosAtUtc",
            table: "negocio_audience_memberships");

        migrationBuilder.DropColumn(
            name: "PermiteCorreosPromocionales",
            table: "negocio_audience_memberships");
    }
}
