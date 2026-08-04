using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Negocios;

[DbContext(typeof(DynamicNegociosDbContext))]
[Migration("20260802103000_LinkExistingWelcomeTickets")]
public class LinkExistingWelcomeTickets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE negocios AS negocio
            SET BonoBienvenidaTicketId = (
                SELECT ticket.Id
                FROM fidelity_tickets AS ticket
                WHERE ticket.NegocioId = negocio.Id
                  AND ticket.EsPlantilla = TRUE
                  AND ticket.UserId IS NULL
                  AND ticket.Activo = TRUE
                  AND ticket.CategoriaEnvioEspecial = 'PrimerRegistro'
                  AND COALESCE(ticket.PuntosCoste, 0) = 0
                  AND (ticket.AvailableFromUtc IS NULL OR ticket.AvailableFromUtc <= UTC_TIMESTAMP(6))
                  AND ticket.ExpiresAtUtc > UTC_TIMESTAMP(6)
                ORDER BY ticket.CreatedAtUtc DESC, ticket.Id DESC
                LIMIT 1
            )
            WHERE negocio.BonoBienvenidaTicketId IS NULL
              AND EXISTS (
                  SELECT 1
                  FROM fidelity_tickets AS ticket
                  WHERE ticket.NegocioId = negocio.Id
                    AND ticket.EsPlantilla = TRUE
                    AND ticket.UserId IS NULL
                    AND ticket.Activo = TRUE
                    AND ticket.CategoriaEnvioEspecial = 'PrimerRegistro'
                    AND COALESCE(ticket.PuntosCoste, 0) = 0
                    AND (ticket.AvailableFromUtc IS NULL OR ticket.AvailableFromUtc <= UTC_TIMESTAMP(6))
                    AND ticket.ExpiresAtUtc > UTC_TIMESTAMP(6)
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No se puede distinguir de forma segura una vinculación creada por esta migración.
    }
}
