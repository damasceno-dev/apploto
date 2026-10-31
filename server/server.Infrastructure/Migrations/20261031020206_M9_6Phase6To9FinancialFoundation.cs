using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_6Phase6To9FinancialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NoMonthlyFinancialRole",
                table: "TransactionTypes");

            migrationBuilder.AddColumn<string>(
                name: "SeedKey",
                table: "TransactionTypes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustodyTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetKind = table.Column<short>(type: "smallint", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustodyTransfers", x => x.Id);
                    table.CheckConstraint("CK_CustodyTransfers_DistinctAccounts", "\"SourceAccountId\" <> \"DestinationAccountId\"");
                    table.CheckConstraint("CK_CustodyTransfers_ValuePositive", "\"Value\" > 0");
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Accounts_DestinationAccountId",
                        column: x => x.DestinationAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Accounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_DailyCloses_SourceSessionId",
                        column: x => x.SourceSessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Transactions_DestinationTransactionId",
                        column: x => x.DestinationTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Transactions_SourceTransactionId",
                        column: x => x.SourceTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionFieldEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<short>(type: "smallint", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CurrentTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionFieldEffects", x => x.Id);
                    table.CheckConstraint("CK_SessionFieldEffects_EffectMatchesValue", "(\"Value\" = 0) = (\"CurrentTransactionId\" IS NULL)");
                    table.CheckConstraint("CK_SessionFieldEffects_FeeNonNegative", "\"Field\" = 1 OR \"Value\" >= 0");
                    table.CheckConstraint("CK_SessionFieldEffects_FieldValid", "\"Field\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_SessionFieldEffects_RevisionPositive", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_SessionFieldEffects_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionFieldEffects_DailyCloses_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionFieldEffects_Transactions_CurrentTransactionId",
                        column: x => x.CurrentTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionFieldEffects_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionFieldEffectRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionFieldEffectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PreviousValue = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreatedTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin = table.Column<short>(type: "smallint", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionFieldEffectRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionFieldEffectRevisions_SessionFieldEffects_SessionFiel~",
                        column: x => x.SessionFieldEffectId,
                        principalTable: "SessionFieldEffects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionFieldEffectRevisions_Transactions_CancelledTransacti~",
                        column: x => x.CancelledTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionFieldEffectRevisions_Transactions_CreatedTransaction~",
                        column: x => x.CreatedTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionFieldEffectRevisions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SessionId_GenerationKind_LiveFieldEffect",
                table: "Transactions",
                columns: new[] { "SessionId", "GenerationKind" },
                unique: true,
                filter: "\"GenerationKind\" IN (3, 4, 5) AND \"Status\" <> 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions",
                sql: "(\"GenerationKind\" IS NULL AND \"GeneratedFromTransactionId\" IS NULL) OR (\"GenerationKind\" IN (1, 2, 6) AND \"GeneratedFromTransactionId\" IS NOT NULL) OR (\"GenerationKind\" IN (3, 4, 5) AND \"GeneratedFromTransactionId\" IS NULL AND \"SessionId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypes_CategoryId_SeedKey",
                table: "TransactionTypes",
                columns: new[] { "CategoryId", "SeedKey" },
                unique: true,
                filter: "\"SeedKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NoMonthlyFinancialRole",
                table: "TransactionTypes",
                sql: "\"SystemRole\" IS NULL OR \"MonthlyFinancialRole\" = CASE \"SystemRole\" WHEN 5 THEN 1 WHEN 6 THEN 3 ELSE 0 END");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_BranchId_DestinationAccountId",
                table: "CustodyTransfers",
                columns: new[] { "BranchId", "DestinationAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_CreatedByUserId",
                table: "CustodyTransfers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_DestinationAccountId",
                table: "CustodyTransfers",
                column: "DestinationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_DestinationTransactionId",
                table: "CustodyTransfers",
                column: "DestinationTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_SourceAccountId",
                table: "CustodyTransfers",
                column: "SourceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_SourceSessionId",
                table: "CustodyTransfers",
                column: "SourceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_SourceTransactionId",
                table: "CustodyTransfers",
                column: "SourceTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionFieldEffectRevisions_ActorUserId",
                table: "SessionFieldEffectRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionFieldEffectRevisions_CancelledTransactionId",
                table: "SessionFieldEffectRevisions",
                column: "CancelledTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionFieldEffectRevisions_CreatedTransactionId",
                table: "SessionFieldEffectRevisions",
                column: "CreatedTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionFieldEffectRevisions_SessionFieldEffectId_Revision",
                table: "SessionFieldEffectRevisions",
                columns: new[] { "SessionFieldEffectId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionFieldEffects_BranchId",
                table: "SessionFieldEffects",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionFieldEffects_CurrentTransactionId",
                table: "SessionFieldEffects",
                column: "CurrentTransactionId",
                unique: true,
                filter: "\"CurrentTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SessionFieldEffects_SessionId_Field",
                table: "SessionFieldEffects",
                columns: new[] { "SessionId", "Field" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionFieldEffects_UpdatedByUserId",
                table: "SessionFieldEffects",
                column: "UpdatedByUserId");

            migrationBuilder.Sql(FinancialFoundationSql.InstallGuards);
            migrationBuilder.Sql(FinancialFoundationSql.InstallSeedFunction);

            // The system fiado-sale type keeps its id and role number; only its label changes (M9.6 Phase 6).
            migrationBuilder.Sql(
                """
                UPDATE "TransactionTypes" SET "Name" = 'Saída de caixa — venda fiado'
                WHERE "SystemRole" = 1 AND "Name" = 'Saída de estoque contado — venda fiado';
                """);

            // Existing branches receive the M9.6 financial seeds only from a reviewed identity map (see
            // database-environments.md). Without one nothing is guessed: their field, deposit and method commands
            // answer BRANCH_FINANCIAL_SEEDS_MISSING until loto_m96_apply_financial_seeds(map) runs or the disposable
            // database is reset. New branches are seeded completely by CreateBranchSeedFactory.
            migrationBuilder.Sql(
                """
                DO $migration$
                DECLARE mapping text := NULLIF(current_setting('loto.m96_financial_seed_map', true), '');
                BEGIN
                    IF mapping IS NOT NULL THEN
                        PERFORM * FROM loto_m96_apply_financial_seeds(mapping::jsonb);
                    END IF;
                END $migration$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only once M9.6 financial data exists: converted types, field effects and transfers cannot be
            // split back into the old shape without losing their history.
            migrationBuilder.Sql(
                """
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SessionFieldEffects") OR EXISTS (SELECT 1 FROM "CustodyTransfers")
                        OR EXISTS (SELECT 1 FROM "TransactionTypes" WHERE "SeedKey" IS NOT NULL OR "SystemRole" > 2)
                        OR EXISTS (SELECT 1 FROM "Transactions" WHERE "GenerationKind" > 2)
                        OR EXISTS (SELECT 1 FROM "Accounts" WHERE "Type" = 3)
                        OR EXISTS (SELECT 1 FROM "CustodyMappingIssues" WHERE "IssueCode" = 2) THEN
                        RAISE EXCEPTION 'M96_FORWARD_ONLY: restore a verified backup or apply a reviewed forward correction';
                    END IF;
                END $$;
                UPDATE "TransactionTypes" SET "Name" = 'Saída de estoque contado — venda fiado'
                WHERE "SystemRole" = 1 AND "Name" = 'Saída de caixa — venda fiado';
                """);
            migrationBuilder.Sql(FinancialFoundationSql.UninstallGuards);

            migrationBuilder.DropTable(
                name: "CustodyTransfers");

            migrationBuilder.DropTable(
                name: "SessionFieldEffectRevisions");

            migrationBuilder.DropTable(
                name: "SessionFieldEffects");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_SessionId_GenerationKind_LiveFieldEffect",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_TransactionTypes_CategoryId_SeedKey",
                table: "TransactionTypes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NoMonthlyFinancialRole",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "SeedKey",
                table: "TransactionTypes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions",
                sql: "(\"GenerationKind\" IS NULL) = (\"GeneratedFromTransactionId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NoMonthlyFinancialRole",
                table: "TransactionTypes",
                sql: "\"SystemRole\" IS NULL OR \"MonthlyFinancialRole\" = 0");
        }
    }
}
