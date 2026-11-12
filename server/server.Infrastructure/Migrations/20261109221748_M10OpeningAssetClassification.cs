using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M10OpeningAssetClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_GoLiveOpeningAsset",
                table: "Transactions",
                sql: "\"GoLiveOpeningId\" IS NULL OR COALESCE((\"Direction\"=1 AND \"SessionId\" IS NULL AND \"GeneratedFromTransactionId\" IS NULL AND \"GenerationKind\" IS NULL AND \"PostedFinancialClass\"=1 AND \"PostedMonthlyFinancialRole\"=0 AND \"PostedSettlementInstrument\" IN (0,1)),false)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_GoLiveOpeningAsset",
                table: "Transactions");
        }
    }
}
