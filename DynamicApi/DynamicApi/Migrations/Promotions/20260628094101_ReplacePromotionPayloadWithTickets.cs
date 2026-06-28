using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Promotions
{
    /// <inheritdoc />
    public partial class ReplacePromotionPayloadWithTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionLabel",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "Conditions",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "DeepLink",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "promotion_campaigns");

            migrationBuilder.AddColumn<string>(
                name: "TicketDescripcionSnapshot",
                table: "promotion_campaigns",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TicketNombreSnapshot",
                table: "promotion_campaigns",
                type: "varchar(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TicketSnapshotJson",
                table: "promotion_campaigns",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "TicketTemplateId",
                table: "promotion_campaigns",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_campaigns_NegocioId_TicketTemplateId",
                table: "promotion_campaigns",
                columns: new[] { "NegocioId", "TicketTemplateId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_promotion_campaigns_NegocioId_TicketTemplateId",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "TicketDescripcionSnapshot",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "TicketNombreSnapshot",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "TicketSnapshotJson",
                table: "promotion_campaigns");

            migrationBuilder.DropColumn(
                name: "TicketTemplateId",
                table: "promotion_campaigns");

            migrationBuilder.AddColumn<string>(
                name: "ActionLabel",
                table: "promotion_campaigns",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Conditions",
                table: "promotion_campaigns",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeepLink",
                table: "promotion_campaigns",
                type: "varchar(1024)",
                maxLength: 1024,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "promotion_campaigns",
                type: "varchar(1024)",
                maxLength: 1024,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "promotion_campaigns",
                type: "varchar(1200)",
                maxLength: 1200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "promotion_campaigns",
                type: "varchar(140)",
                maxLength: 140,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
