using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity;

[DbContext(typeof(DynamicFidelityDbContext))]
[Migration("20260701143000_SimplifyTicketTypes")]
public partial class SimplifyTicketTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE `fidelity_tickets`
            SET `Tipo` = CASE `Tipo`
                WHEN 'DescuentoPorcentual' THEN 'Porcentual'
                WHEN 'DescuentoImporteFijo' THEN 'ValorFijo'
                WHEN 'Regalo' THEN 'Libre'
                WHEN 'DosPorUno' THEN 'Libre'
                WHEN 'Especial' THEN 'Libre'
                ELSE `Tipo`
            END
            WHERE `Tipo` IN (
                'DescuentoPorcentual',
                'DescuentoImporteFijo',
                'Regalo',
                'DosPorUno',
                'Especial'
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE `fidelity_tickets`
            SET `Tipo` = CASE `Tipo`
                WHEN 'Porcentual' THEN 'DescuentoPorcentual'
                WHEN 'ValorFijo' THEN 'DescuentoImporteFijo'
                WHEN 'Libre' THEN 'Especial'
                ELSE `Tipo`
            END
            WHERE `Tipo` IN ('Porcentual', 'ValorFijo', 'Libre');
            """);
    }
}
