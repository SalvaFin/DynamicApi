using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddWelcomeTicketClaimGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fidelity_welcome_ticket_claims",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TicketId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_welcome_ticket_claims", x => new { x.UserId, x.NegocioId });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Preserve one historical claim per customer and business without deleting
            // existing tickets. Extra historical tickets remain available for audit.
            migrationBuilder.Sql("""
                INSERT INTO fidelity_welcome_ticket_claims (UserId, NegocioId, TicketId, CreatedAtUtc)
                SELECT ticket.UserId, ticket.NegocioId, ticket.Id, ticket.CreatedAtUtc
                FROM fidelity_tickets AS ticket
                WHERE ticket.UserId IS NOT NULL
                  AND ticket.EsPlantilla = 0
                  AND ticket.CategoriaEnvioEspecial = 'PrimerRegistro'
                  AND ticket.Id = (
                      SELECT previous.Id FROM fidelity_tickets AS previous
                      WHERE previous.UserId = ticket.UserId
                        AND previous.NegocioId = ticket.NegocioId
                        AND previous.EsPlantilla = 0
                        AND previous.CategoriaEnvioEspecial = 'PrimerRegistro'
                      ORDER BY previous.CreatedAtUtc, previous.Id
                      LIMIT 1)
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO fidelity_welcome_ticket_claims (UserId, NegocioId, TicketId, CreatedAtUtc)
                SELECT pending.UserId, pending.NegocioId, pending.AssignedTicketId,
                       COALESCE(pending.ActivatedAtUtc, pending.CreatedAtUtc)
                FROM fidelity_pending_ticket_assignments AS pending
                WHERE pending.Activated = 1 AND pending.AssignedTicketId IS NOT NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fidelity_welcome_ticket_claims");
        }
    }
}
