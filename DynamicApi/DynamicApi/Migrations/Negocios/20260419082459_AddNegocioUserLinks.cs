using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Negocios
{
    /// <inheritdoc />
    public partial class AddNegocioUserLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "negocio_user_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TipoVinculacion = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TituloRelacion = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Activa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EsPrincipal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PuedeAccederBackoffice = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PuedeGestionarNegocio = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PuedeGestionarClientes = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PuedeGestionarCampanas = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PuedeGestionarPuntos = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PuedeValidarTickets = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PuedeVerReportes = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NotasInternas = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrigenVinculacion = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LinkedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UnlinkedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FechaInvitacionUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FechaAceptacionUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FechaInicioUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FechaFinUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_negocio_user_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_negocio_user_links_negocios_NegocioId",
                        column: x => x.NegocioId,
                        principalTable: "negocios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_negocio_user_links_Activa",
                table: "negocio_user_links",
                column: "Activa");

            migrationBuilder.CreateIndex(
                name: "IX_negocio_user_links_NegocioId_UserId",
                table: "negocio_user_links",
                columns: new[] { "NegocioId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_negocio_user_links_TipoVinculacion",
                table: "negocio_user_links",
                column: "TipoVinculacion");

            migrationBuilder.CreateIndex(
                name: "IX_negocio_user_links_UserId",
                table: "negocio_user_links",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "negocio_user_links");
        }
    }
}
