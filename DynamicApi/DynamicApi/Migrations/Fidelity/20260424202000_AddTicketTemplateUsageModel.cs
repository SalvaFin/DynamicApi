using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    [DbContext(typeof(DynamicFidelityDbContext))]
    [Migration("20260424202000_AddTicketTemplateUsageModel")]
    public partial class AddTicketTemplateUsageModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxUsosPorCliente",
                table: "fidelity_tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsosConsumidos",
                table: "fidelity_tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ValidezDiasDesdeAsignacion",
                table: "fidelity_tickets",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxUsosPorCliente",
                table: "fidelity_tickets");

            migrationBuilder.DropColumn(
                name: "UsosConsumidos",
                table: "fidelity_tickets");

            migrationBuilder.DropColumn(
                name: "ValidezDiasDesdeAsignacion",
                table: "fidelity_tickets");
        }
    }
}
