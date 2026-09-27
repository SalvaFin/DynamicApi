using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddAccrualIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientOperationId",
                table: "fidelity_points_transactions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_transactions_NegocioId_ClientOperationId",
                table: "fidelity_points_transactions",
                columns: new[] { "NegocioId", "ClientOperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fidelity_points_transactions_NegocioId_ClientOperationId",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "ClientOperationId",
                table: "fidelity_points_transactions");
        }
    }
}
