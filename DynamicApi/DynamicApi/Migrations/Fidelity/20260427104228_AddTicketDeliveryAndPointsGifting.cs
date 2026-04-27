using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddTicketDeliveryAndPointsGifting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoriaEnvioEspecial",
                table: "fidelity_tickets",
                type: "varchar(48)",
                maxLength: 48,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CounterpartyUserCodeSnapshot",
                table: "fidelity_points_transactions",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "CounterpartyUserId",
                table: "fidelity_points_transactions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_CategoriaEnvioEspecial",
                table: "fidelity_tickets",
                column: "CategoriaEnvioEspecial");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_transactions_CounterpartyUserId",
                table: "fidelity_points_transactions",
                column: "CounterpartyUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fidelity_tickets_CategoriaEnvioEspecial",
                table: "fidelity_tickets");

            migrationBuilder.DropIndex(
                name: "IX_fidelity_points_transactions_CounterpartyUserId",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "CategoriaEnvioEspecial",
                table: "fidelity_tickets");

            migrationBuilder.DropColumn(
                name: "CounterpartyUserCodeSnapshot",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "CounterpartyUserId",
                table: "fidelity_points_transactions");
        }
    }
}
