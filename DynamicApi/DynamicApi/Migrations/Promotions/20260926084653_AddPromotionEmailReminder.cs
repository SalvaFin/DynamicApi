using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Promotions
{
    /// <inheritdoc />
    public partial class AddPromotionEmailReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RecipientId is covered by the existing FK. MariaDB will not drop the
            // supporting single-column index while that constraint is present.
            migrationBuilder.DropForeignKey(
                name: "FK_promotion_email_deliveries_promotion_recipients_RecipientId",
                table: "promotion_email_deliveries");

            migrationBuilder.DropIndex(
                name: "IX_promotion_email_deliveries_RecipientId",
                table: "promotion_email_deliveries");

            migrationBuilder.AddColumn<bool>(
                name: "IsReminder",
                table: "promotion_email_deliveries",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderQueuedAtUtc",
                table: "promotion_campaigns",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_promotion_email_deliveries_RecipientId_IsReminder",
                table: "promotion_email_deliveries",
                columns: new[] { "RecipientId", "IsReminder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_promotion_email_deliveries_promotion_recipients_RecipientId",
                table: "promotion_email_deliveries",
                column: "RecipientId",
                principalTable: "promotion_recipients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_promotion_email_deliveries_promotion_recipients_RecipientId",
                table: "promotion_email_deliveries");

            migrationBuilder.DropIndex(
                name: "IX_promotion_email_deliveries_RecipientId_IsReminder",
                table: "promotion_email_deliveries");

            migrationBuilder.DropColumn(
                name: "IsReminder",
                table: "promotion_email_deliveries");

            migrationBuilder.DropColumn(
                name: "ReminderQueuedAtUtc",
                table: "promotion_campaigns");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_email_deliveries_RecipientId",
                table: "promotion_email_deliveries",
                column: "RecipientId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_promotion_email_deliveries_promotion_recipients_RecipientId",
                table: "promotion_email_deliveries",
                column: "RecipientId",
                principalTable: "promotion_recipients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
