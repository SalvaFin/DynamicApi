using Dynamic.Promotions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Promotions;

[DbContext(typeof(DynamicPromotionsDbContext))]
[Migration("20260801140000_AddPromotionPresentationTracking")]
public sealed class AddPromotionPresentationTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "PresentedAtUtc",
            table: "promotion_recipients",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_promotion_recipients_UserId_PresentedAtUtc_ExpiresAtUtc",
            table: "promotion_recipients",
            columns: new[] { "UserId", "PresentedAtUtc", "ExpiresAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_promotion_recipients_UserId_PresentedAtUtc_ExpiresAtUtc",
            table: "promotion_recipients");

        migrationBuilder.DropColumn(
            name: "PresentedAtUtc",
            table: "promotion_recipients");
    }
}
