using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M10PolymorphicImportTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoLiveImportTargets_Transactions_TargetId",
                table: "GoLiveImportTargets");

            migrationBuilder.DropIndex(
                name: "IX_GoLiveImportTargets_TargetId",
                table: "GoLiveImportTargets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_GoLiveImportTargets_TargetId",
                table: "GoLiveImportTargets",
                column: "TargetId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoLiveImportTargets_Transactions_TargetId",
                table: "GoLiveImportTargets",
                column: "TargetId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
