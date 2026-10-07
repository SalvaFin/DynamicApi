using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DynamicApi.Migrations.Fidelity;

[DbContext(typeof(DynamicFidelityDbContext))]
[Migration("20261001120000_EnforceWelcomeTicketLifetimeGuard")]
public sealed class EnforceWelcomeTicketLifetimeGuard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Keep historical duplicates for audit, but reserve every historical welcome.
        migrationBuilder.Sql("""
            INSERT IGNORE INTO fidelity_welcome_ticket_claims (UserId, NegocioId, TicketId, CreatedAtUtc)
            SELECT ticket.UserId, ticket.NegocioId, ticket.Id, ticket.CreatedAtUtc
            FROM fidelity_tickets ticket
            WHERE ticket.UserId IS NOT NULL AND ticket.EsPlantilla = 0
              AND ticket.CategoriaEnvioEspecial = 'PrimerRegistro'
            ORDER BY ticket.CreatedAtUtc, ticket.Id
            """);

        // A service may reserve the claim before inserting its ticket. Direct writers
        // reserve it here instead. The composite key serializes both routes.
        migrationBuilder.Sql("""
            CREATE TRIGGER fidelity_welcome_ticket_before_insert
            BEFORE INSERT ON fidelity_tickets FOR EACH ROW
            BEGIN
                DECLARE claimed_ticket CHAR(36);
                IF NEW.UserId IS NOT NULL AND NEW.EsPlantilla = 0
                   AND NEW.CategoriaEnvioEspecial = 'PrimerRegistro' THEN
                    INSERT INTO fidelity_welcome_ticket_claims (UserId, NegocioId, TicketId, CreatedAtUtc)
                    VALUES (NEW.UserId, NEW.NegocioId, NEW.Id, NEW.CreatedAtUtc)
                    ON DUPLICATE KEY UPDATE TicketId = TicketId;
                    SELECT TicketId INTO claimed_ticket FROM fidelity_welcome_ticket_claims
                    WHERE UserId = NEW.UserId AND NegocioId = NEW.NegocioId FOR UPDATE;
                    IF claimed_ticket <> NEW.Id THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'welcome_already_claimed: Solo una bienvenida por cliente y negocio.';
                    END IF;
                END IF;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER fidelity_welcome_ticket_before_update
            BEFORE UPDATE ON fidelity_tickets FOR EACH ROW
            BEGIN
                DECLARE claimed_ticket CHAR(36);
                IF OLD.UserId IS NOT NULL AND OLD.EsPlantilla = 0
                   AND OLD.CategoriaEnvioEspecial = 'PrimerRegistro' THEN
                    IF NOT (NEW.Id <=> OLD.Id) OR NOT (NEW.UserId <=> OLD.UserId)
                       OR NOT (NEW.NegocioId <=> OLD.NegocioId)
                       OR NEW.EsPlantilla <> OLD.EsPlantilla
                       OR NEW.CategoriaEnvioEspecial <> OLD.CategoriaEnvioEspecial THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'welcome_identity_immutable: No se puede reasignar una bienvenida emitida.';
                    END IF;
                ELSEIF NEW.UserId IS NOT NULL AND NEW.EsPlantilla = 0
                       AND NEW.CategoriaEnvioEspecial = 'PrimerRegistro' THEN
                    INSERT INTO fidelity_welcome_ticket_claims (UserId, NegocioId, TicketId, CreatedAtUtc)
                    VALUES (NEW.UserId, NEW.NegocioId, NEW.Id, NEW.CreatedAtUtc)
                    ON DUPLICATE KEY UPDATE TicketId = TicketId;
                    SELECT TicketId INTO claimed_ticket FROM fidelity_welcome_ticket_claims
                    WHERE UserId = NEW.UserId AND NegocioId = NEW.NegocioId FOR UPDATE;
                    IF claimed_ticket <> NEW.Id THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'welcome_already_claimed: Solo una bienvenida por cliente y negocio.';
                    END IF;
                END IF;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS fidelity_welcome_ticket_before_update");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS fidelity_welcome_ticket_before_insert");
    }
}
