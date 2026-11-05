using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7TypedBankMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions");

            migrationBuilder.CreateTable(
                name: "BankMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    BankReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankMovements_BankReceipts_BankReceiptId",
                        column: x => x.BankReceiptId,
                        principalTable: "BankReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankMovements_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankMovements_Transactions_BankTransactionId",
                        column: x => x.BankTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankMovements_Transactions_SourceTransactionId",
                        column: x => x.SourceTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankMovements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions",
                sql: "(\"GenerationKind\" IS NULL AND \"GeneratedFromTransactionId\" IS NULL) OR (\"GenerationKind\" IN (1, 2, 6, 7) AND \"GeneratedFromTransactionId\" IS NOT NULL) OR (\"GenerationKind\" = 8 AND \"GeneratedFromTransactionId\" IS NULL AND \"SessionId\" IS NULL) OR (\"GenerationKind\" IN (3, 4, 5) AND \"GeneratedFromTransactionId\" IS NULL AND \"SessionId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_BankMovements_BankReceiptId",
                table: "BankMovements",
                column: "BankReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankMovements_BankTransactionId",
                table: "BankMovements",
                column: "BankTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankMovements_BranchId",
                table: "BankMovements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BankMovements_CreatedByUserId",
                table: "BankMovements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankMovements_SourceTransactionId",
                table: "BankMovements",
                column: "SourceTransactionId",
                unique: true,
                filter: "\"SourceTransactionId\" IS NOT NULL");
            migrationBuilder.Sql(TypedBankMovementsSql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS (SELECT 1 FROM "BankMovements") THEN RAISE EXCEPTION 'M97_BANKING_DOWNGRADE: typed banking history exists'; END IF; END $$;
                DROP FUNCTION bank_movement_coherent() CASCADE;
                DROP FUNCTION bank_movement_transaction_immutable() CASCADE;
                """);
            migrationBuilder.Sql(CardBankingSql.Install[..CardBankingSql.Install.IndexOf("CREATE FUNCTION bank_match_coherent", StringComparison.Ordinal)]);
            migrationBuilder.DropTable(
                name: "BankMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions",
                sql: "(\"GenerationKind\" IS NULL AND \"GeneratedFromTransactionId\" IS NULL) OR (\"GenerationKind\" IN (1, 2, 6, 7) AND \"GeneratedFromTransactionId\" IS NOT NULL) OR (\"GenerationKind\" IN (3, 4, 5) AND \"GeneratedFromTransactionId\" IS NULL AND \"SessionId\" IS NOT NULL)");
        }
    }
}
