using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddRecurrenceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BasePointsSnapshot",
                table: "fidelity_points_transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseRatioSnapshot",
                table: "fidelity_points_transactions",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BenefitMultiplierSnapshot",
                table: "fidelity_points_transactions",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurrenceSnapshotJson",
                table: "fidelity_points_transactions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "VisitOrdinalSnapshot",
                table: "fidelity_points_transactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fidelity_recurrence_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Family = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BenefitMode = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Compatibility = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Threshold = table.Column<int>(type: "int", nullable: false),
                    WindowDays = table.Column<int>(type: "int", nullable: true),
                    BenefitVisits = table.Column<int>(type: "int", nullable: true),
                    BenefitDays = table.Column<int>(type: "int", nullable: true),
                    Multiplier = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_recurrence_rules", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_recurrence_rules_NegocioId_Active_Priority",
                table: "fidelity_recurrence_rules",
                columns: new[] { "NegocioId", "Active", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fidelity_recurrence_rules");

            migrationBuilder.DropColumn(
                name: "BasePointsSnapshot",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "BaseRatioSnapshot",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "BenefitMultiplierSnapshot",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "RecurrenceSnapshotJson",
                table: "fidelity_points_transactions");

            migrationBuilder.DropColumn(
                name: "VisitOrdinalSnapshot",
                table: "fidelity_points_transactions");
        }
    }
}
