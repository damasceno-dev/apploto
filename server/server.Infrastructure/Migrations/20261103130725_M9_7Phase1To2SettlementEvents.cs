using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7Phase1To2SettlementEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_BranchId_DueDate",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClassifiedAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "ClassificationProvenance",
                table: "Transactions",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClassificationRevision",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<short>(
                name: "PostedFinancialClass",
                table: "Transactions",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "PostedMonthlyFinancialRole",
                table: "Transactions",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "PostedSettlementInstrument",
                table: "Transactions",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "FinancialClass",
                table: "TransactionTypes",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "BankReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "date", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReceipts", x => x.Id);
                    table.CheckConstraint("CK_BankReceipts_AmountPositive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_BankReceipts_PostedOnOrAfterOccurred", "\"PostedOn\" >= \"OccurredOn\"");
                    table.ForeignKey(
                        name: "FK_BankReceipts_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankReceipts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankReceipts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionClassificationRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PreviousSettlementInstrument = table.Column<short>(type: "smallint", nullable: true),
                    PreviousMonthlyFinancialRole = table.Column<short>(type: "smallint", nullable: true),
                    PreviousFinancialClass = table.Column<short>(type: "smallint", nullable: true),
                    PreviousProvenance = table.Column<short>(type: "smallint", nullable: true),
                    SettlementInstrument = table.Column<short>(type: "smallint", nullable: false),
                    MonthlyFinancialRole = table.Column<short>(type: "smallint", nullable: false),
                    FinancialClass = table.Column<short>(type: "smallint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionClassificationRevisions", x => x.Id);
                    table.CheckConstraint("CK_TransactionClassificationRevisions_RevisionAfterPosting", "\"Revision\" >= 2");
                    table.ForeignKey(
                        name: "FK_TransactionClassificationRevisions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionClassificationRevisions_Transactions_Transaction~",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionClassificationRevisions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SettlementEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivableTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    PreviousEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    OutstandingDelta = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "date", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Provenance = table.Column<short>(type: "smallint", nullable: false),
                    MigrationIssue = table.Column<short>(type: "smallint", nullable: false),
                    SourcePaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SettlementInstrument = table.Column<short>(type: "smallint", nullable: false),
                    BankReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversesEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustodyFromAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustodyToAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextExpectedDepositDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReturnReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConvertedTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementEvents", x => x.Id);
                    table.CheckConstraint("CK_SettlementEvents_AmountPositive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_SettlementEvents_Chain", "\"Sequence\" >= 1 AND ((\"Sequence\" = 1) = (\"PreviousEventId\" IS NULL))");
                    table.CheckConstraint("CK_SettlementEvents_KindDetails", "(\"Kind\" = 3) = (\"BankReceiptId\" IS NOT NULL) AND (\"Kind\" = 4) = (\"ReversesEventId\" IS NOT NULL) AND (\"Kind\" = 6) = (\"ConvertedTransactionId\" IS NOT NULL) AND (\"Kind\" = 2) = (\"CustodyToAccountId\" IS NOT NULL) AND (\"Kind\" <> 4 OR (\"ClientId\" IS NOT NULL AND \"NextExpectedDepositDate\" IS NOT NULL)) AND (\"Kind\" <> 6 OR \"ClientId\" IS NOT NULL) AND (\"Kind\" = 2 OR \"CustodyFromAccountId\" IS NULL) AND (\"ReturnReason\" IS NULL OR \"Kind\" = 4)");
                    table.CheckConstraint("CK_SettlementEvents_OutstandingDelta", "CASE \"Kind\" WHEN 1 THEN \"OutstandingDelta\" = -\"Amount\" OR (\"OutstandingDelta\" = 0 AND (\"MigrationIssue\" & 1) = 1) WHEN 3 THEN \"OutstandingDelta\" = -\"Amount\" WHEN 6 THEN \"OutstandingDelta\" = -\"Amount\" WHEN 4 THEN \"OutstandingDelta\" = \"Amount\" WHEN 2 THEN \"OutstandingDelta\" = 0 WHEN 5 THEN \"OutstandingDelta\" = 0 ELSE false END");
                    table.CheckConstraint("CK_SettlementEvents_PostedOnOrAfterOccurred", "\"PostedOn\" >= \"OccurredOn\"");
                    table.CheckConstraint("CK_SettlementEvents_Provenance", "(\"Provenance\" = 1 AND \"Kind\" <> 1 AND \"CreatedByUserId\" IS NOT NULL AND \"SourcePaidAt\" IS NULL AND \"MigrationIssue\" = 0) OR (\"Provenance\" = 2 AND \"Kind\" = 1 AND \"Sequence\" = 1 AND \"CreatedByUserId\" IS NULL AND \"SourcePaidAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SettlementEvents_Accounts_CustodyFromAccountId",
                        column: x => x.CustodyFromAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementEvents_Accounts_CustodyToAccountId",
                        column: x => x.CustodyToAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementEvents_BankReceipts_BankReceiptId",
                        column: x => x.BankReceiptId,
                        principalTable: "BankReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementEvents_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementEvents_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementEvents_SettlementEvents_PreviousEventId",
                        column: x => x.PreviousEventId,
                        principalTable: "SettlementEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementEvents_SettlementEvents_ReversesEventId",
                        column: x => x.ReversesEventId,
                        principalTable: "SettlementEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementEvents_Transactions_ConvertedTransactionId",
                        column: x => x.ConvertedTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementEvents_Transactions_ReceivableTransactionId",
                        column: x => x.ReceivableTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementEvents_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Upgrade order: system types and their fixed class, the frozen-classification backfill, the PaidAt
            // reconstruction (reads the backfilled instrument), then PaidAt is dropped and the guards installed.
            migrationBuilder.Sql(SettlementEventsSql.SystemTypes);
            migrationBuilder.Sql(SettlementEventsSql.BackfillClassification);
            migrationBuilder.Sql(SettlementEventsSql.ReconstructPaidAt);

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BranchId_DueDate",
                table: "Transactions",
                columns: new[] { "BranchId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BranchId_PostedSettlementInstrument_DueDate",
                table: "Transactions",
                columns: new[] { "BranchId", "PostedSettlementInstrument", "DueDate" },
                filter: "\"Status\" = 1 AND \"PostedSettlementInstrument\" <> 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_ActiveIsClassified",
                table: "Transactions",
                sql: "\"Status\" <> 1 OR \"ClassificationRevision\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_ClassificationSnapshot",
                table: "Transactions",
                sql: "(\"ClassificationRevision\" = 0 AND \"ClassificationProvenance\" IS NULL AND \"PostedSettlementInstrument\" IS NULL AND \"PostedMonthlyFinancialRole\" IS NULL AND \"PostedFinancialClass\" IS NULL AND \"ClassifiedAt\" IS NULL) OR (\"ClassificationRevision\" >= 1 AND \"ClassificationProvenance\" IS NOT NULL AND \"PostedSettlementInstrument\" IS NOT NULL AND \"PostedMonthlyFinancialRole\" IS NOT NULL AND \"PostedFinancialClass\" IS NOT NULL AND \"ClassifiedAt\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions",
                sql: "(\"GenerationKind\" IS NULL AND \"GeneratedFromTransactionId\" IS NULL) OR (\"GenerationKind\" IN (1, 2, 6, 7) AND \"GeneratedFromTransactionId\" IS NOT NULL) OR (\"GenerationKind\" IN (3, 4, 5) AND \"GeneratedFromTransactionId\" IS NULL AND \"SessionId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_FinancialClass",
                table: "TransactionTypes",
                sql: "\"SystemRole\" IS NULL OR \"FinancialClass\" = CASE \"SystemRole\" WHEN 1 THEN 1 WHEN 2 THEN 1 WHEN 3 THEN 1 WHEN 4 THEN 1 WHEN 5 THEN 2 WHEN 6 THEN 2 WHEN 7 THEN 4 WHEN 8 THEN 4 WHEN 9 THEN 1 END");

            migrationBuilder.CreateIndex(
                name: "IX_BankReceipts_BankAccountId",
                table: "BankReceipts",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankReceipts_BranchId_BankAccountId_Status",
                table: "BankReceipts",
                columns: new[] { "BranchId", "BankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BankReceipts_CreatedByUserId",
                table: "BankReceipts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_BankReceiptId",
                table: "SettlementEvents",
                column: "BankReceiptId",
                filter: "\"BankReceiptId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_BranchId_PostedOn",
                table: "SettlementEvents",
                columns: new[] { "BranchId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_ClientId",
                table: "SettlementEvents",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_ConvertedTransactionId",
                table: "SettlementEvents",
                column: "ConvertedTransactionId",
                unique: true,
                filter: "\"ConvertedTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_CreatedByUserId",
                table: "SettlementEvents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_CustodyFromAccountId",
                table: "SettlementEvents",
                column: "CustodyFromAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_CustodyToAccountId",
                table: "SettlementEvents",
                column: "CustodyToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_PreviousEventId",
                table: "SettlementEvents",
                column: "PreviousEventId",
                unique: true,
                filter: "\"PreviousEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_ReceivableTransactionId_Migrated",
                table: "SettlementEvents",
                column: "ReceivableTransactionId",
                unique: true,
                filter: "\"Provenance\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_ReceivableTransactionId_Sequence",
                table: "SettlementEvents",
                columns: new[] { "ReceivableTransactionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementEvents_ReversesEventId",
                table: "SettlementEvents",
                column: "ReversesEventId",
                unique: true,
                filter: "\"ReversesEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationRevisions_BatchId",
                table: "TransactionClassificationRevisions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationRevisions_BranchId",
                table: "TransactionClassificationRevisions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationRevisions_CreatedByUserId",
                table: "TransactionClassificationRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionClassificationRevisions_TransactionId_Revision",
                table: "TransactionClassificationRevisions",
                columns: new[] { "TransactionId", "Revision" },
                unique: true);

            migrationBuilder.Sql(SettlementEventsSql.InstallGuards);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SettlementEventsSql.DowngradePreflight);
            migrationBuilder.Sql(SettlementEventsSql.UninstallGuards);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(SettlementEventsSql.RestorePaidAt);

            migrationBuilder.DropTable(
                name: "SettlementEvents");

            migrationBuilder.DropTable(
                name: "TransactionClassificationRevisions");

            migrationBuilder.DropTable(
                name: "BankReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BranchId_DueDate",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BranchId_PostedSettlementInstrument_DueDate",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_ActiveIsClassified",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_ClassificationSnapshot",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_FinancialClass",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "ClassificationProvenance",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ClassificationRevision",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PostedFinancialClass",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PostedMonthlyFinancialRole",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PostedSettlementInstrument",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FinancialClass",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "ClassifiedAt",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BranchId_DueDate",
                table: "Transactions",
                columns: new[] { "BranchId", "DueDate" },
                filter: "\"PaidAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions",
                sql: "(\"GenerationKind\" IS NULL AND \"GeneratedFromTransactionId\" IS NULL) OR (\"GenerationKind\" IN (1, 2, 6) AND \"GeneratedFromTransactionId\" IS NOT NULL) OR (\"GenerationKind\" IN (3, 4, 5) AND \"GeneratedFromTransactionId\" IS NULL AND \"SessionId\" IS NOT NULL)");
        }
    }
}
