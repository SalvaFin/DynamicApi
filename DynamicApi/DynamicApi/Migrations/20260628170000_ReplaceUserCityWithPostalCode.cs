using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations;

[DbContext(typeof(DynamicUsersDbContext))]
[Migration("20260628170000_ReplaceUserCityWithPostalCode")]
public partial class ReplaceUserCityWithPostalCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "City",
            table: "users");

        migrationBuilder.AddColumn<string>(
            name: "PostalCode",
            table: "users",
            type: "varchar(24)",
            maxLength: 24,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_users_PostalCode",
            table: "users",
            column: "PostalCode");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_users_PostalCode",
            table: "users");

        migrationBuilder.DropColumn(
            name: "PostalCode",
            table: "users");

        migrationBuilder.AddColumn<string>(
            name: "City",
            table: "users",
            type: "varchar(128)",
            maxLength: 128,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }
}
