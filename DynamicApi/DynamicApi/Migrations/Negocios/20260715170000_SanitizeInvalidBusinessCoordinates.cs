using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Negocios;

[DbContext(typeof(DynamicNegociosDbContext))]
[Migration("20260715170000_SanitizeInvalidBusinessCoordinates")]
public class SanitizeInvalidBusinessCoordinates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE negocios
            SET Latitud = NULL, Longitud = NULL
            WHERE (Latitud IS NULL AND Longitud IS NOT NULL)
               OR (Latitud IS NOT NULL AND Longitud IS NULL)
               OR Latitud < -90
               OR Latitud > 90
               OR Longitud < -180
               OR Longitud > 180;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Los valores inválidos originales no deben restaurarse.
    }
}
