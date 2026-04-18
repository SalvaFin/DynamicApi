using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoStepUserRegistrationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldMaxLength: 256)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldMaxLength: 256)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "AgeAtRegistration",
                table: "users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhoneNumber",
                table: "users",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "PhoneNumberConfirmed",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RegistrationCompleted",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationCompletedAtUtc",
                table: "users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationInitiatedAtUtc",
                table: "users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationValidationToken",
                table: "users",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationValidationTokenExpiresAtUtc",
                table: "users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TemporaryPasswordSentAtUtc",
                table: "users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_NormalizedPhoneNumber",
                table: "users",
                column: "NormalizedPhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_RegistrationValidationToken",
                table: "users",
                column: "RegistrationValidationToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_NormalizedPhoneNumber",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_RegistrationValidationToken",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AgeAtRegistration",
                table: "users");

            migrationBuilder.DropColumn(
                name: "NormalizedPhoneNumber",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PhoneNumberConfirmed",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RegistrationCompleted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RegistrationCompletedAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RegistrationInitiatedAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RegistrationValidationToken",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RegistrationValidationTokenExpiresAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TemporaryPasswordSentAtUtc",
                table: "users");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "NormalizedEmail",
                keyValue: null,
                column: "NormalizedEmail",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldMaxLength: 256,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Email",
                keyValue: null,
                column: "Email",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldMaxLength: 256,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
