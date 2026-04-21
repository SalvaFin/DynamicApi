using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Negocios
{
    /// <inheritdoc />
    public partial class AddNegocioPointsConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveMaestraLocalHash",
                table: "negocios",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaveMaestraLocalUpdatedAtUtc",
                table: "negocios",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RatioConversionEurosAPuntos",
                table: "negocios",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaveMaestraLocalHash",
                table: "negocios");

            migrationBuilder.DropColumn(
                name: "ClaveMaestraLocalUpdatedAtUtc",
                table: "negocios");

            migrationBuilder.DropColumn(
                name: "RatioConversionEurosAPuntos",
                table: "negocios");
        }
    }
}
