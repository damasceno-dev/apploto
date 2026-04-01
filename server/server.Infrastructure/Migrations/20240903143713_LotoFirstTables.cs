using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class LotoFirstTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FluxoClassificaçãos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    PlanoDeContas = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FluxoClassificaçãos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FluxoContas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Identificação = table.Column<string>(type: "text", nullable: false),
                    Instituição = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FluxoContas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fluxos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataLançamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric", nullable: false),
                    ContaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificaçãoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fluxos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fluxos_FluxoClassificaçãos_ClassificaçãoId",
                        column: x => x.ClassificaçãoId,
                        principalTable: "FluxoClassificaçãos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Fluxos_FluxoContas_ContaId",
                        column: x => x.ContaId,
                        principalTable: "FluxoContas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FluxoDetalhamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false),
                    FluxoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FluxoDetalhamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FluxoDetalhamentos_Fluxos_FluxoId",
                        column: x => x.FluxoId,
                        principalTable: "Fluxos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FluxoDetalhamentos_FluxoId",
                table: "FluxoDetalhamentos",
                column: "FluxoId");

            migrationBuilder.CreateIndex(
                name: "IX_Fluxos_ClassificaçãoId",
                table: "Fluxos",
                column: "ClassificaçãoId");

            migrationBuilder.CreateIndex(
                name: "IX_Fluxos_ContaId",
                table: "Fluxos",
                column: "ContaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FluxoDetalhamentos");

            migrationBuilder.DropTable(
                name: "Fluxos");

            migrationBuilder.DropTable(
                name: "FluxoClassificaçãos");

            migrationBuilder.DropTable(
                name: "FluxoContas");
        }
    }
}
