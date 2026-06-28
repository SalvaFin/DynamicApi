using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddPromotionCampaignTicketSourceToFidelity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourcePromotionCampaignId",
                table: "fidelity_tickets",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePromotionRecipientId",
                table: "fidelity_tickets",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_SourcePromotionCampaignId",
                table: "fidelity_tickets",
                column: "SourcePromotionCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_SourcePromotionRecipientId",
                table: "fidelity_tickets",
                column: "SourcePromotionRecipientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fidelity_tickets_SourcePromotionCampaignId",
                table: "fidelity_tickets");

            migrationBuilder.DropIndex(
                name: "IX_fidelity_tickets_SourcePromotionRecipientId",
                table: "fidelity_tickets");

            migrationBuilder.DropColumn(
                name: "SourcePromotionCampaignId",
                table: "fidelity_tickets");

            migrationBuilder.DropColumn(
                name: "SourcePromotionRecipientId",
                table: "fidelity_tickets");
        }
    }
}
