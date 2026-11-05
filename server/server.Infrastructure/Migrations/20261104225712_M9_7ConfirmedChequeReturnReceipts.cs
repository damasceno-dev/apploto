using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7ConfirmedChequeReturnReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceSettlementEventId",
                table: "BankReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankReceipts_SourceSettlementEventId",
                table: "BankReceipts",
                column: "SourceSettlementEventId",
                unique: true,
                filter: "\"SourceSettlementEventId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_BankReceipts_SettlementEvents_SourceSettlementEventId",
                table: "BankReceipts",
                column: "SourceSettlementEventId",
                principalTable: "SettlementEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(ConfirmedChequeReturnsSql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS (SELECT 1 FROM "BankReceipts" WHERE "SourceSettlementEventId" IS NOT NULL) THEN RAISE EXCEPTION 'M97_BANKING_DOWNGRADE: cheque-return evidence exists'; END IF; END $$;
                DROP FUNCTION bank_receipt_source_immutable() CASCADE;
                """);
            migrationBuilder.Sql(TypedBankMovementsSql.Install[..TypedBankMovementsSql.Install.IndexOf("CREATE FUNCTION bank_movement_coherent", StringComparison.Ordinal)]);
            migrationBuilder.DropForeignKey(
                name: "FK_BankReceipts_SettlementEvents_SourceSettlementEventId",
                table: "BankReceipts");

            migrationBuilder.DropIndex(
                name: "IX_BankReceipts_SourceSettlementEventId",
                table: "BankReceipts");

            migrationBuilder.DropColumn(
                name: "SourceSettlementEventId",
                table: "BankReceipts");
        }
    }
}
