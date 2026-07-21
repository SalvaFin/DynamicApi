using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Promotions
{
    /// <inheritdoc />
    public partial class AddPromotionEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailDeliveredCount",
                table: "promotion_campaigns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmailEligibleCount",
                table: "promotion_campaigns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EmailEnabled",
                table: "promotion_campaigns",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EmailFailedCount",
                table: "promotion_campaigns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NegocioAddressSnapshot",
                table: "promotion_campaigns",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "NegocioLatitudeSnapshot",
                table: "promotion_campaigns",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NegocioLongitudeSnapshot",
                table: "promotion_campaigns",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "promotion_email_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CampaignId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RecipientId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecipientName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnsubscribeToken = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastError = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_email_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_promotion_email_deliveries_promotion_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "promotion_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_promotion_email_deliveries_promotion_recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "promotion_recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_email_deliveries_CampaignId_Status",
                table: "promotion_email_deliveries",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_promotion_email_deliveries_RecipientId",
                table: "promotion_email_deliveries",
                column: "RecipientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promotion_email_deliveries_Status_NextAttemptAtUtc",
                table: "promotion_email_deliveries",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_promotion_email_deliveries_UnsubscribeToken",
                table: "promotion_email_deliveries",
                column: "UnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotion_email_deliveries");

            migrationBuilder.DropColumn(
                name: "EmailDeliveredCount",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "EmailEligibleCount",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "EmailEnabled",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "EmailFailedCount",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "NegocioAddressSnapshot",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "NegocioLatitudeSnapshot",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "NegocioLongitudeSnapshot",
                table: "promotion_campaigns");
        }
    }
}
