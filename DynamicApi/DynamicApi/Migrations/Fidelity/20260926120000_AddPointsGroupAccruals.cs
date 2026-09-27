using System;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity;

[DbContext(typeof(DynamicFidelityDbContext))]
[Migration("20260926120000_AddPointsGroupAccruals")]
public partial class AddPointsGroupAccruals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "fidelity_points_group_accruals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                IdempotencyKey = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                WorkerUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                AmountEuros = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                TotalPoints = table.Column<int>(type: "int", nullable: false),
                RecipientCount = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_fidelity_points_group_accruals", x => x.Id))
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "fidelity_points_group_accrual_recipients",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                GroupAccrualId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ScanOrder = table.Column<int>(type: "int", nullable: false),
                PointsAssigned = table.Column<int>(type: "int", nullable: false),
                TransactionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
            }, constraints: table =>
            {
                table.PrimaryKey("PK_fidelity_points_group_accrual_recipients", x => x.Id);
                table.ForeignKey("FK_fidelity_points_group_accrual_recipients_fidelity_points_group_accruals_GroupAccrualId",
                    x => x.GroupAccrualId, "fidelity_points_group_accruals", "Id", onDelete: ReferentialAction.Cascade);
            }).Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex("IX_fidelity_points_group_accruals_IdempotencyKey", "fidelity_points_group_accruals", "IdempotencyKey", unique: true);
        migrationBuilder.CreateIndex("IX_fidelity_points_group_accrual_recipients_GroupAccrualId_UserId", "fidelity_points_group_accrual_recipients", new[] { "GroupAccrualId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_fidelity_points_group_accrual_recipients_TransactionId", "fidelity_points_group_accrual_recipients", "TransactionId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("fidelity_points_group_accrual_recipients");
        migrationBuilder.DropTable("fidelity_points_group_accruals");
    }
}
