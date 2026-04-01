using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class RenameFluxoClassificações : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fluxos_FluxoClassificaçãos_ClassificaçãoId",
                table: "Fluxos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FluxoClassificaçãos",
                table: "FluxoClassificaçãos");

            migrationBuilder.RenameTable(
                name: "FluxoClassificaçãos",
                newName: "FluxoClassificações");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FluxoClassificações",
                table: "FluxoClassificações",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Fluxos_FluxoClassificações_ClassificaçãoId",
                table: "Fluxos",
                column: "ClassificaçãoId",
                principalTable: "FluxoClassificações",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fluxos_FluxoClassificações_ClassificaçãoId",
                table: "Fluxos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FluxoClassificações",
                table: "FluxoClassificações");

            migrationBuilder.RenameTable(
                name: "FluxoClassificações",
                newName: "FluxoClassificaçãos");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FluxoClassificaçãos",
                table: "FluxoClassificaçãos",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Fluxos_FluxoClassificaçãos_ClassificaçãoId",
                table: "Fluxos",
                column: "ClassificaçãoId",
                principalTable: "FluxoClassificaçãos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
