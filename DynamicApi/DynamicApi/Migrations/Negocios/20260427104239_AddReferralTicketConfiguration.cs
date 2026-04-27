using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Negocios
{
    /// <inheritdoc />
    public partial class AddReferralTicketConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BonoInvitacionNuevoClienteTicketId",
                table: "negocios",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_negocios_BonoInvitacionNuevoClienteTicketId",
                table: "negocios",
                column: "BonoInvitacionNuevoClienteTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_negocios_BonoInvitacionNuevoClienteTicketId",
                table: "negocios");

            migrationBuilder.DropColumn(
                name: "BonoInvitacionNuevoClienteTicketId",
                table: "negocios");
        }
    }
}
