using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7CardBatchesAndStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SettlementEvents_KindDetails",
                table: "SettlementEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SettlementEvents_OutstandingDelta",
                table: "SettlementEvents");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "BankReceipts",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "BankStatementImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Original = table.Column<byte[]>(type: "bytea", nullable: false),
                    From = table.Column<DateTime>(type: "date", nullable: false),
                    To = table.Column<DateTime>(type: "date", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementImports_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementImports_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementImports_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    BankIdentity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    Memo = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    Original = table.Column<string>(type: "text", nullable: false),
                    AmbiguousIdentity = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CardProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalIdentity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Brand = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DelayDays = table.Column<int>(type: "integer", nullable: false),
                    BusinessDays = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardProviders", x => x.Id);
                    table.CheckConstraint("CK_CardProviders_Delay", "\"DelayDays\" BETWEEN 0 AND 366");
                    table.ForeignKey(
                        name: "FK_CardProviders_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardProviders_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardProviders_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankMatchProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineVersion = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptVersion = table.Column<long>(type: "bigint", nullable: false),
                    ExpectationFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankMatchProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankMatchProposals_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankMatchProposals_BankReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "BankReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankMatchProposals_BankStatementLines_LineId",
                        column: x => x.LineId,
                        principalTable: "BankStatementLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankMatchProposals_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankMatchProposals_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementImportLines",
                columns: table => new
                {
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementImportLines", x => new { x.ImportId, x.LineId });
                    table.ForeignKey(
                        name: "FK_BankStatementImportLines_BankStatementImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "BankStatementImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementImportLines_BankStatementLines_LineId",
                        column: x => x.LineId,
                        principalTable: "BankStatementLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CardSettlementBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalIdentity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BankReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Brand = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlannedArrivalOn = table.Column<DateTime>(type: "date", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ConfirmedFee = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    FeeBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FeeEvidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ManagerConfirmedFee = table.Column<bool>(type: "boolean", nullable: false),
                    ObservedNetAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    FeeFinancialClass = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardSettlementBatches", x => x.Id);
                    table.CheckConstraint("CK_CardSettlementBatches_Amounts", "\"GrossAmount\" > 0 AND (\"ConfirmedFee\" IS NULL OR (\"ConfirmedFee\" >= 0 AND \"ConfirmedFee\" < \"GrossAmount\")) AND (\"ObservedNetAmount\" IS NULL OR \"ObservedNetAmount\" > 0)");
                    table.ForeignKey(
                        name: "FK_CardSettlementBatches_BankReceipts_BankReceiptId",
                        column: x => x.BankReceiptId,
                        principalTable: "BankReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardSettlementBatches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardSettlementBatches_CardProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "CardProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardSettlementBatches_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementMatches_BankMatchProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "BankMatchProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementMatches_BankReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "BankReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementMatches_BankStatementLines_LineId",
                        column: x => x.LineId,
                        principalTable: "BankStatementLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementMatches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementMatches_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettlementEvents_KindDetails",
                table: "SettlementEvents",
                sql: "(\"Kind\" IN (3, 7)) = (\"BankReceiptId\" IS NOT NULL) AND (\"Kind\" = 4) = (\"ReversesEventId\" IS NOT NULL) AND (\"Kind\" = 6) = (\"ConvertedTransactionId\" IS NOT NULL) AND (\"Kind\" = 2) = (\"CustodyToAccountId\" IS NOT NULL) AND (\"Kind\" <> 4 OR (\"ClientId\" IS NOT NULL AND \"NextExpectedDepositDate\" IS NOT NULL)) AND (\"Kind\" <> 6 OR \"ClientId\" IS NOT NULL) AND (\"Kind\" = 2 OR \"CustodyFromAccountId\" IS NULL) AND (\"ReturnReason\" IS NULL OR \"Kind\" = 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettlementEvents_OutstandingDelta",
                table: "SettlementEvents",
                sql: "CASE \"Kind\" WHEN 1 THEN \"OutstandingDelta\" = -\"Amount\" OR (\"OutstandingDelta\" = 0 AND (\"MigrationIssue\" & 1) = 1) WHEN 3 THEN \"OutstandingDelta\" = -\"Amount\" WHEN 7 THEN \"OutstandingDelta\" = -\"Amount\" WHEN 6 THEN \"OutstandingDelta\" = -\"Amount\" WHEN 4 THEN \"OutstandingDelta\" = \"Amount\" WHEN 2 THEN \"OutstandingDelta\" = 0 WHEN 5 THEN \"OutstandingDelta\" = 0 ELSE false END");

            migrationBuilder.CreateIndex(
                name: "IX_BankMatchProposals_BankAccountId",
                table: "BankMatchProposals",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankMatchProposals_BranchId",
                table: "BankMatchProposals",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BankMatchProposals_CreatedByUserId",
                table: "BankMatchProposals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankMatchProposals_LineId",
                table: "BankMatchProposals",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_BankMatchProposals_ReceiptId",
                table: "BankMatchProposals",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImportLines_LineId",
                table: "BankStatementImportLines",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_BankAccountId",
                table: "BankStatementImports",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_BranchId_BankAccountId_Fingerprint",
                table: "BankStatementImports",
                columns: new[] { "BranchId", "BankAccountId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_CreatedByUserId",
                table: "BankStatementImports",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_BankAccountId",
                table: "BankStatementLines",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_BranchId_BankAccountId_BankIdentity",
                table: "BankStatementLines",
                columns: new[] { "BranchId", "BankAccountId", "BankIdentity" },
                unique: true,
                filter: "\"BankIdentity\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementMatches_BranchId",
                table: "BankStatementMatches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementMatches_CreatedByUserId",
                table: "BankStatementMatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementMatches_LineId",
                table: "BankStatementMatches",
                column: "LineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementMatches_ProposalId",
                table: "BankStatementMatches",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementMatches_ReceiptId",
                table: "BankStatementMatches",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardProviders_BankAccountId",
                table: "CardProviders",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CardProviders_BranchId_ExternalIdentity",
                table: "CardProviders",
                columns: new[] { "BranchId", "ExternalIdentity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardProviders_CreatedByUserId",
                table: "CardProviders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CardSettlementBatches_BankReceiptId",
                table: "CardSettlementBatches",
                column: "BankReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardSettlementBatches_BranchId",
                table: "CardSettlementBatches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CardSettlementBatches_CreatedByUserId",
                table: "CardSettlementBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CardSettlementBatches_ProviderId_ExternalIdentity",
                table: "CardSettlementBatches",
                columns: new[] { "ProviderId", "ExternalIdentity" },
                unique: true);
            migrationBuilder.Sql(CardBankingSql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CardBankingSql.PreflightDown);
            migrationBuilder.DropTable(
                name: "BankStatementImportLines");

            migrationBuilder.DropTable(
                name: "BankStatementMatches");

            migrationBuilder.DropTable(
                name: "CardSettlementBatches");

            migrationBuilder.DropTable(
                name: "BankStatementImports");

            migrationBuilder.DropTable(
                name: "BankMatchProposals");

            migrationBuilder.DropTable(
                name: "CardProviders");

            migrationBuilder.DropTable(
                name: "BankStatementLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SettlementEvents_KindDetails",
                table: "SettlementEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SettlementEvents_OutstandingDelta",
                table: "SettlementEvents");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "BankReceipts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettlementEvents_KindDetails",
                table: "SettlementEvents",
                sql: "(\"Kind\" = 3) = (\"BankReceiptId\" IS NOT NULL) AND (\"Kind\" = 4) = (\"ReversesEventId\" IS NOT NULL) AND (\"Kind\" = 6) = (\"ConvertedTransactionId\" IS NOT NULL) AND (\"Kind\" = 2) = (\"CustodyToAccountId\" IS NOT NULL) AND (\"Kind\" <> 4 OR (\"ClientId\" IS NOT NULL AND \"NextExpectedDepositDate\" IS NOT NULL)) AND (\"Kind\" <> 6 OR \"ClientId\" IS NOT NULL) AND (\"Kind\" = 2 OR \"CustodyFromAccountId\" IS NULL) AND (\"ReturnReason\" IS NULL OR \"Kind\" = 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettlementEvents_OutstandingDelta",
                table: "SettlementEvents",
                sql: "CASE \"Kind\" WHEN 1 THEN \"OutstandingDelta\" = -\"Amount\" OR (\"OutstandingDelta\" = 0 AND (\"MigrationIssue\" & 1) = 1) WHEN 3 THEN \"OutstandingDelta\" = -\"Amount\" WHEN 6 THEN \"OutstandingDelta\" = -\"Amount\" WHEN 4 THEN \"OutstandingDelta\" = \"Amount\" WHEN 2 THEN \"OutstandingDelta\" = 0 WHEN 5 THEN \"OutstandingDelta\" = 0 ELSE false END");
        }
    }
}
