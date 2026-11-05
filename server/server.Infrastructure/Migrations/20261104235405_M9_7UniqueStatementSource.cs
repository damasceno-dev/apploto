using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7UniqueStatementSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankStatementAccountBindings_BranchId",
                table: "BankStatementAccountBindings");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementAccountBindings_BranchId_BankIdentity_AccountI~",
                table: "BankStatementAccountBindings",
                columns: new[] { "BranchId", "BankIdentity", "AccountIdentity" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankStatementAccountBindings_BranchId_BankIdentity_AccountI~",
                table: "BankStatementAccountBindings");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementAccountBindings_BranchId",
                table: "BankStatementAccountBindings",
                column: "BranchId");
        }
    }
}
