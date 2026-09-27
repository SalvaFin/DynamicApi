using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddPointsSplits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fidelity_points_splits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceTransactionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OwnerUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OriginalPoints = table.Column<int>(type: "int", nullable: false),
                    OwnerShare = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_points_splits", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fidelity_points_split_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SplitId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PointsAmount = table.Column<int>(type: "int", nullable: false),
                    IncomingTransactionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_points_split_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fidelity_points_split_recipients_fidelity_points_splits_Spli~",
                        column: x => x.SplitId,
                        principalTable: "fidelity_points_splits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_split_recipients_IncomingTransactionId",
                table: "fidelity_points_split_recipients",
                column: "IncomingTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_split_recipients_SplitId_UserId",
                table: "fidelity_points_split_recipients",
                columns: new[] { "SplitId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_splits_OwnerUserId_CreatedAtUtc",
                table: "fidelity_points_splits",
                columns: new[] { "OwnerUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_splits_SourceTransactionId",
                table: "fidelity_points_splits",
                column: "SourceTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fidelity_points_split_recipients");

            migrationBuilder.DropTable(
                name: "fidelity_points_splits");
        }
    }
}
