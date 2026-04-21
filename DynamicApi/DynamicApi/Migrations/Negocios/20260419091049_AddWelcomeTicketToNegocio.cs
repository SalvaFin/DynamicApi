using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Negocios
{
    /// <inheritdoc />
    public partial class AddWelcomeTicketToNegocio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BonoBienvenidaTicketId",
                table: "negocios",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_negocios_BonoBienvenidaTicketId",
                table: "negocios",
                column: "BonoBienvenidaTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_negocios_BonoBienvenidaTicketId",
                table: "negocios");

            migrationBuilder.DropColumn(
                name: "BonoBienvenidaTicketId",
                table: "negocios");
        }
    }
}
