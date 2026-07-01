using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddTicketRedemptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fidelity_ticket_redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TicketId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ValidatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ParentTicketId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SourceQrCampaignId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SourcePromotionCampaignId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SourcePromotionRecipientId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TicketNombreSnapshot = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TicketTipoSnapshot = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TicketCategoriaSnapshot = table.Column<string>(type: "varchar(48)", maxLength: 48, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TicketCodeSnapshot = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UsageNumber = table.Column<int>(type: "int", nullable: false),
                    PurchaseAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    FinalAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    MinimumSpendSatisfied = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    StoreReference = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_ticket_redemptions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_CreatedAtUtc",
                table: "fidelity_ticket_redemptions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_NegocioId",
                table: "fidelity_ticket_redemptions",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_NegocioId_CreatedAtUtc",
                table: "fidelity_ticket_redemptions",
                columns: new[] { "NegocioId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_ParentTicketId",
                table: "fidelity_ticket_redemptions",
                column: "ParentTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_SourcePromotionCampaignId",
                table: "fidelity_ticket_redemptions",
                column: "SourcePromotionCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_SourcePromotionRecipientId",
                table: "fidelity_ticket_redemptions",
                column: "SourcePromotionRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_SourceQrCampaignId",
                table: "fidelity_ticket_redemptions",
                column: "SourceQrCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_TicketId",
                table: "fidelity_ticket_redemptions",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_TicketId_UsageNumber",
                table: "fidelity_ticket_redemptions",
                columns: new[] { "TicketId", "UsageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_UserId",
                table: "fidelity_ticket_redemptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_ticket_redemptions_ValidatedByUserId",
                table: "fidelity_ticket_redemptions",
                column: "ValidatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fidelity_ticket_redemptions");
        }
    }
}
