using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7ReceiptSourceInvariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_BankReceipts_SourceEvent",
                table: "BankReceipts",
                sql: "(\"SourceKind\" = 6) = (\"SourceSettlementEventId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BankReceipts_SourceEvent",
                table: "BankReceipts");
        }
    }
}
