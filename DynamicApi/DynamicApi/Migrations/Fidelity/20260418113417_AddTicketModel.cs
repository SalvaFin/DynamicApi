using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicApi.Migrations.Fidelity
{
    /// <inheritdoc />
    public partial class AddTicketModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fidelity_tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NegocioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Nombre = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descripcion = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tipo = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CodigoInterno = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CodigoVisible = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TituloCanje = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InstruccionesCanje = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CondicionesUso = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MensajeMarketing = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DescuentoPorcentaje = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    DescuentoImporteFijo = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    BeneficioEspecialResumen = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BeneficioEspecialDetalle = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GastoMinimoRequerido = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PuntosCoste = table.Column<int>(type: "int", nullable: true),
                    RequiereValidacionManual = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EsDeUnSoloUso = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Publicado = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Usado = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AvailableFromUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UsedInStoreReference = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UsedByEmployeeReference = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NotasInternas = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fidelity_tickets", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_ExpiresAtUtc",
                table: "fidelity_tickets",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_NegocioId",
                table: "fidelity_tickets",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_NegocioId_CodigoVisible",
                table: "fidelity_tickets",
                columns: new[] { "NegocioId", "CodigoVisible" });

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_Tipo",
                table: "fidelity_tickets",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_Usado",
                table: "fidelity_tickets",
                column: "Usado");

            migrationBuilder.CreateIndex(
                name: "IX_fidelity_tickets_UserId",
                table: "fidelity_tickets",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fidelity_tickets");
        }
    }
}
