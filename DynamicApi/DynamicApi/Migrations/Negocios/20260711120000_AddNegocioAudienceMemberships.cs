using System;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Negocios
{
    [DbContext(typeof(DynamicNegociosDbContext))]
    [Migration("20260711120000_AddNegocioAudienceMemberships")]
    public partial class AddNegocioAudienceMemberships : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "negocio_audience_memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Activa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EsFavorito = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OrigenAlta = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    UltimaActividadOrigen = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    FechaAltaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaBajaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UltimaActividadUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_negocio_audience_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_negocio_audience_memberships_negocios_NegocioId",
                        column: x => x.NegocioId,
                        principalTable: "negocios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_negocio_audience_memberships_Activa",
                table: "negocio_audience_memberships",
                column: "Activa");

            migrationBuilder.CreateIndex(
                name: "IX_negocio_audience_memberships_EsFavorito",
                table: "negocio_audience_memberships",
                column: "EsFavorito");

            migrationBuilder.CreateIndex(
                name: "IX_negocio_audience_memberships_NegocioId_UserId",
                table: "negocio_audience_memberships",
                columns: new[] { "NegocioId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_negocio_audience_memberships_UltimaActividadUtc",
                table: "negocio_audience_memberships",
                column: "UltimaActividadUtc");

            migrationBuilder.CreateIndex(
                name: "IX_negocio_audience_memberships_UserId",
                table: "negocio_audience_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_negocio_audience_memberships_UserId_Activa_UltimaActividadUtc",
                table: "negocio_audience_memberships",
                columns: new[] { "UserId", "Activa", "UltimaActividadUtc" });

            migrationBuilder.Sql(
                """
                INSERT IGNORE INTO `negocio_audience_memberships`
                    (`Id`, `NegocioId`, `UserId`, `Activa`, `EsFavorito`, `OrigenAlta`, `UltimaActividadOrigen`,
                     `FechaAltaUtc`, `FechaBajaUtc`, `UltimaActividadUtc`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), legacy_link.`NegocioId`, legacy_link.`UserId`,
                       legacy_link.`Activa`,
                       0,
                       COALESCE(NULLIF(legacy_link.`OrigenVinculacion`, ''), 'legacy_cliente_link'),
                       COALESCE(NULLIF(legacy_link.`OrigenVinculacion`, ''), 'legacy_cliente_link'),
                       COALESCE(legacy_link.`FechaAceptacionUtc`, legacy_link.`FechaInicioUtc`, legacy_link.`CreatedAtUtc`, UTC_TIMESTAMP(6)),
                       CASE
                           WHEN legacy_link.`Activa` = 1 AND legacy_link.`RevokedAtUtc` IS NULL THEN NULL
                           ELSE COALESCE(legacy_link.`RevokedAtUtc`, legacy_link.`FechaFinUtc`)
                       END,
                       COALESCE(legacy_link.`UpdatedAtUtc`, legacy_link.`CreatedAtUtc`, UTC_TIMESTAMP(6)),
                       COALESCE(legacy_link.`CreatedAtUtc`, UTC_TIMESTAMP(6)),
                       UTC_TIMESTAMP(6)
                FROM `negocio_user_links` legacy_link
                WHERE legacy_link.`TipoVinculacion` = 'Cliente';
                """);

            migrationBuilder.Sql(
                """
                SET @has_fidelity_points := (
                    SELECT COUNT(*)
                    FROM information_schema.tables
                    WHERE table_schema = DATABASE() AND table_name = 'fidelity_points'
                );
                SET @points_seed_sql := IF(@has_fidelity_points > 0,
                    'INSERT IGNORE INTO `negocio_audience_memberships`
                        (`Id`, `NegocioId`, `UserId`, `Activa`, `EsFavorito`, `OrigenAlta`, `UltimaActividadOrigen`,
                         `FechaAltaUtc`, `FechaBajaUtc`, `UltimaActividadUtc`, `CreatedAtUtc`, `UpdatedAtUtc`)
                     SELECT UUID(), points_source.`NegocioId`, points_source.`UserId`, 1, 0,
                            ''legacy_points_activity'', ''legacy_points_activity'',
                            MIN(points_source.`CreatedAtUtc`), NULL,
                            MAX(COALESCE(points_source.`LastMovementAtUtc`, points_source.`UpdatedAtUtc`, points_source.`CreatedAtUtc`)),
                            MIN(points_source.`CreatedAtUtc`), UTC_TIMESTAMP(6)
                     FROM `fidelity_points` points_source
                     GROUP BY points_source.`NegocioId`, points_source.`UserId`',
                    'SELECT 1'
                );
                PREPARE points_seed_stmt FROM @points_seed_sql;
                EXECUTE points_seed_stmt;
                DEALLOCATE PREPARE points_seed_stmt;
                """);

            migrationBuilder.Sql(
                """
                SET @has_fidelity_tickets := (
                    SELECT COUNT(*)
                    FROM information_schema.tables
                    WHERE table_schema = DATABASE() AND table_name = 'fidelity_tickets'
                );
                SET @tickets_seed_sql := IF(@has_fidelity_tickets > 0,
                    'INSERT IGNORE INTO `negocio_audience_memberships`
                        (`Id`, `NegocioId`, `UserId`, `Activa`, `EsFavorito`, `OrigenAlta`, `UltimaActividadOrigen`,
                         `FechaAltaUtc`, `FechaBajaUtc`, `UltimaActividadUtc`, `CreatedAtUtc`, `UpdatedAtUtc`)
                     SELECT UUID(), ticket_source.`NegocioId`, ticket_source.`UserId`, 1, 0,
                            ''legacy_ticket_activity'', ''legacy_ticket_activity'',
                            MIN(ticket_source.`CreatedAtUtc`), NULL,
                            MAX(COALESCE(ticket_source.`UpdatedAtUtc`, ticket_source.`CreatedAtUtc`)),
                            MIN(ticket_source.`CreatedAtUtc`), UTC_TIMESTAMP(6)
                     FROM `fidelity_tickets` ticket_source
                     WHERE ticket_source.`UserId` IS NOT NULL
                     GROUP BY ticket_source.`NegocioId`, ticket_source.`UserId`',
                    'SELECT 1'
                );
                PREPARE tickets_seed_stmt FROM @tickets_seed_sql;
                EXECUTE tickets_seed_stmt;
                DEALLOCATE PREPARE tickets_seed_stmt;
                """);

            migrationBuilder.Sql("DELETE FROM `negocio_user_links` WHERE `TipoVinculacion` = 'Cliente';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT IGNORE INTO `negocio_user_links`
                    (`Id`, `NegocioId`, `UserId`, `TipoVinculacion`, `TituloRelacion`, `Activa`, `EsPrincipal`,
                     `PuedeAccederBackoffice`, `PuedeGestionarNegocio`, `PuedeGestionarClientes`, `PuedeGestionarCampanas`,
                     `PuedeGestionarPuntos`, `PuedeValidarTickets`, `PuedeVerReportes`, `OrigenVinculacion`,
                     `FechaInvitacionUtc`, `FechaAceptacionUtc`, `FechaInicioUtc`, `FechaFinUtc`, `CreatedAtUtc`, `UpdatedAtUtc`, `RevokedAtUtc`)
                SELECT UUID(), audience.`NegocioId`, audience.`UserId`, 'Cliente', 'Cliente', audience.`Activa`, 0,
                       0, 0, 0, 0, 0, 0, 0, audience.`OrigenAlta`,
                       audience.`FechaAltaUtc`, audience.`FechaAltaUtc`, audience.`FechaAltaUtc`, audience.`FechaBajaUtc`,
                       audience.`CreatedAtUtc`, audience.`UpdatedAtUtc`, audience.`FechaBajaUtc`
                FROM `negocio_audience_memberships` audience;
                """);

            migrationBuilder.DropTable(
                name: "negocio_audience_memberships");
        }
    }
}
