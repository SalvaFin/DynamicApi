using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddRecentPointsEarningsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_fidelity_points_transactions_UserId_CreatedAtUtc_Id",
                table: "fidelity_points_transactions",
                columns: new[] { "UserId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fidelity_points_transactions_UserId_CreatedAtUtc_Id",
                table: "fidelity_points_transactions");
        }
    }
}
