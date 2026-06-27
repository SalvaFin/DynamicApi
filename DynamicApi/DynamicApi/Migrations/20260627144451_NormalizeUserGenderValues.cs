using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserGenderValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE `users`
                SET `Gender` = CASE `Gender`
                    WHEN 'Male' THEN 'Hombre'
                    WHEN 'Female' THEN 'Mujer'
                    WHEN 'Hombre' THEN 'Hombre'
                    WHEN 'Mujer' THEN 'Mujer'
                    ELSE 'OtroPrefieroNoEspecificar'
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "users",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "OtroPrefieroNoEspecificar",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE `users`
                SET `Gender` = CASE `Gender`
                    WHEN 'Hombre' THEN 'Male'
                    WHEN 'Mujer' THEN 'Female'
                    ELSE 'PreferNotToSay'
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "users",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldDefaultValue: "OtroPrefieroNoEspecificar")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
