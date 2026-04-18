using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class InitialFidelityPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fidelity_points",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CurrentBalance = table.Column<int>(type: "int", nullable: false),
                    TotalEarned = table.Column<int>(type: "int", nullable: false),
                    TotalSpent = table.Column<int>(type: "int", nullable: false),
                    PendingBalance = table.Column<int>(type: "int", nullable: false),
                    ExpiredBalance = table.Column<int>(type: "int", nullable: false),
                    LastEarnedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastSpentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastMovementAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastReason = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastReference = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_points", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_NegocioId",
                table: "fidelity_points",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_UserId",
                table: "fidelity_points",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_UserId_NegocioId",
                table: "fidelity_points",
                columns: new[] { "UserId", "NegocioId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fidelity_points");
        }
    }
}
