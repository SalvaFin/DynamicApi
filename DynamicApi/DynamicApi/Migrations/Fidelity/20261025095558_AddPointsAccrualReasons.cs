using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddPointsAccrualReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccrualReasonId",
                table: "fidelity_points_transactions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "UniqueAccrualReasonId",
                table: "fidelity_points_transactions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ReasonId",
                table: "fidelity_points_group_accruals",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "fidelity_points_accrual_reasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UniquePerCustomer = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Active = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_points_accrual_reasons", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                INSERT INTO fidelity_points_accrual_reasons (Id, NegocioId, Name, UniquePerCustomer, Active)
                SELECT Id, Id, 'Paso por caja', 0, 1 FROM negocios;

                UPDATE fidelity_points_transactions
                SET AccrualReasonId = NegocioId
                WHERE TransactionType IN ('Earn', 'BackofficeEarn')
                  AND Reason IN ('Paso por caja', 'Consumo en el local', 'Compra en tienda',
                    'Compra registrada por trabajador', 'Compra escaneada por trabajador',
                    'Compra de grupo escaneada por trabajador', 'Compra validada en local',
                    'Acreditación directa de backoffice', 'Acreditacion directa de trabajador');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_points_accrual_reason_history",
                table: "fidelity_points_transactions",
                columns: new[] { "NegocioId", "UserId", "AccrualReasonId" });

            migrationBuilder.CreateIndex(
                name: "UX_points_unique_accrual_reason",
                table: "fidelity_points_transactions",
                columns: new[] { "NegocioId", "UserId", "UniqueAccrualReasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_accrual_reasons_NegocioId_Name",
                table: "fidelity_points_accrual_reasons",
                columns: new[] { "NegocioId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fidelity_points_accrual_reasons");

            migrationBuilder.DropIndex(
                name: "IX_points_accrual_reason_history",
                table: "fidelity_points_transactions");

            migrationBuilder.DropIndex(
                name: "UX_points_unique_accrual_reason",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "AccrualReasonId",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "UniqueAccrualReasonId",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "ReasonId",
                table: "fidelity_points_group_accruals");
        }
    }
}
