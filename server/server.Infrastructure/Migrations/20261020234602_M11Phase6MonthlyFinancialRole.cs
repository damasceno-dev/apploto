using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M11Phase6MonthlyFinancialRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // M11 Phase 6.1: every existing type stays None (0). The Tarifa Bolão and Sobra markers are explicit
            // configuration by type identity; customized existing objects are never guessed from their names.
            migrationBuilder.AddColumn<short>(
                name: "MonthlyFinancialRole",
                table: "TransactionTypes",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NoMonthlyFinancialRole",
                table: "TransactionTypes",
                sql: "\"SystemRole\" IS NULL OR \"MonthlyFinancialRole\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NoMonthlyFinancialRole",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "MonthlyFinancialRole",
                table: "TransactionTypes");
        }
    }
}
