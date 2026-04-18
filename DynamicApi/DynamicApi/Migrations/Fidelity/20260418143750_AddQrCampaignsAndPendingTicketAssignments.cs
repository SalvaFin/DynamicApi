using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddQrCampaignsAndPendingTicketAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsPlantilla",
                table: "fidelity_tickets",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentTicketId",
                table: "fidelity_tickets",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceQrCampaignId",
                table: "fidelity_tickets",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "fidelity_pending_ticket_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    QrCampaignId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TicketTemplateId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AssignedTicketId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    QrToken = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Activated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_pending_ticket_assignments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fidelity_qr_campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WelcomeTicketTemplateId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Nombre = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Token = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descripcion = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LandingPath = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Activa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Visible = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UnSoloUsoPorUsuario = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Expira = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AvailableFromUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_qr_campaigns", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_EsPlantilla",
                table: "fidelity_tickets",
                column: "EsPlantilla");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_ParentTicketId",
                table: "fidelity_tickets",
                column: "ParentTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_pending_ticket_assignments_Activated",
                table: "fidelity_pending_ticket_assignments",
                column: "Activated");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_pending_ticket_assignments_QrCampaignId",
                table: "fidelity_pending_ticket_assignments",
                column: "QrCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_pending_ticket_assignments_UserId",
                table: "fidelity_pending_ticket_assignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_pending_ticket_assignments_UserId_QrCampaignId",
                table: "fidelity_pending_ticket_assignments",
                columns: new[] { "UserId", "QrCampaignId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_qr_campaigns_Activa",
                table: "fidelity_qr_campaigns",
                column: "Activa");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_qr_campaigns_NegocioId",
                table: "fidelity_qr_campaigns",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_qr_campaigns_Token",
                table: "fidelity_qr_campaigns",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fidelity_pending_ticket_assignments");

            migrationBuilder.DropTable(
                name: "fidelity_qr_campaigns");

            migrationBuilder.DropIndex(
                name: "IX_fidelity_tickets_EsPlantilla",
                table: "fidelity_tickets");

            migrationBuilder.DropIndex(
                name: "IX_fidelity_tickets_ParentTicketId",
                table: "fidelity_tickets");

            migrationBuilder.DropColumn(
                name: "EsPlantilla",
                table: "fidelity_tickets");

            migrationBuilder.DropColumn(
                name: "ParentTicketId",
                table: "fidelity_tickets");

            migrationBuilder.DropColumn(
                name: "SourceQrCampaignId",
                table: "fidelity_tickets");
        }
    }
}
