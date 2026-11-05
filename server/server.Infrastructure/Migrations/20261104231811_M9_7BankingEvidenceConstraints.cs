using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7BankingEvidenceConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_CardSettlementBatches_FeeEvidence",
                table: "CardSettlementBatches",
                sql: "\"FeeFinancialClass\" = 3 AND ((\"ConfirmedFee\" IS NULL AND NOT \"ManagerConfirmedFee\" AND \"FeeBasis\" IS NULL AND \"FeeEvidence\" IS NULL) OR (\"ConfirmedFee\" IS NOT NULL AND \"ManagerConfirmedFee\" AND length(btrim(\"FeeBasis\")) > 0 AND length(btrim(\"FeeEvidence\")) > 0 AND \"FeeBasis\" IS NOT NULL AND \"FeeEvidence\" IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CardProviders_Instrument",
                table: "CardProviders",
                sql: "\"Instrument\" IN (2, 3, 4, 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CardSettlementBatches_FeeEvidence",
                table: "CardSettlementBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CardProviders_Instrument",
                table: "CardProviders");
        }
    }
}
