using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7_7Phase5FiadoLinkedMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedFromTransactionId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "GenerationKind",
                table: "Transactions",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "FiadoPaymentKind",
                table: "TransactionTypes",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "SystemRole",
                table: "TransactionTypes",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_GeneratedFromTransactionId",
                table: "Transactions",
                column: "GeneratedFromTransactionId",
                unique: true,
                filter: "\"GeneratedFromTransactionId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions",
                sql: "(\"GenerationKind\" IS NULL) = (\"GeneratedFromTransactionId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NotPaymentMethod",
                table: "TransactionTypes",
                sql: "\"SystemRole\" IS NULL OR \"FiadoPaymentKind\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Transactions_GeneratedFromTransactionId",
                table: "Transactions",
                column: "GeneratedFromTransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Every existing branch receives the two system-owned counterpart types (and the dedicated categories
            // that classify them) exactly as CreateBranchSeedFactory gives a new branch. Seeding never touches existing
            // configuration: each branch gets new category rows of its own (a branch that already uses the default
            // name for an active category gets a disambiguated name, so the active-name unique index cannot fail), and
            // a branch that already has a system type of a role is skipped. No historical row gains a generation
            // origin and no repayment method is guessed for existing types.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE m7_7_phase5_fiado_seed AS
                SELECT b."Id" AS "BranchId", seed."Role", seed."Direction", seed."CategoryName", seed."TypeName",
                       gen_random_uuid() AS "CategoryId", gen_random_uuid() AS "TypeId"
                FROM "Branches" b
                CROSS JOIN (VALUES
                    (1::smallint, 1, 'Movimentos automáticos de fiado (saída)', 'Saída de estoque contado — venda fiado'),
                    (2::smallint, 0, 'Movimentos automáticos de fiado (entrada)', 'Entrada de dinheiro — recebimento fiado'))
                    AS seed("Role", "Direction", "CategoryName", "TypeName")
                WHERE NOT EXISTS (
                    SELECT 1 FROM "TransactionTypes" existing
                    INNER JOIN "Categories" ec ON ec."Id" = existing."CategoryId"
                    WHERE ec."BranchId" = b."Id" AND existing."SystemRole" = seed."Role");

                INSERT INTO "Categories" ("Id", "Name", "DefaultDirection", "BranchId", "Active", "CreatedAt")
                SELECT s."CategoryId",
                       CASE WHEN EXISTS (
                                SELECT 1 FROM "Categories" c
                                WHERE c."BranchId" = s."BranchId" AND c."Active" AND c."Name" = s."CategoryName")
                            THEN s."CategoryName" || ' — sistema ' || left(s."CategoryId"::text, 8)
                            ELSE s."CategoryName" END,
                       s."Direction", s."BranchId", true, now()
                FROM m7_7_phase5_fiado_seed s;

                INSERT INTO "TransactionTypes"
                    ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId", "Active", "CreatedAt", "SystemRole")
                SELECT s."TypeId", s."TypeName", 0, false, s."CategoryId", true, now(), s."Role"
                FROM m7_7_phase5_fiado_seed s;

                DROP TABLE m7_7_phase5_fiado_seed;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove only what this migration owns: the system-owned types, and the categories that host them and
            // nothing else (they are read-only, so no ordinary type can have joined them). Every pre-existing
            // category — active or not, whatever its name — is left untouched.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE m7_7_phase5_fiado_owned AS
                SELECT DISTINCT t."CategoryId" AS "CategoryId"
                FROM "TransactionTypes" t
                WHERE t."SystemRole" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM "TransactionTypes" other
                      WHERE other."CategoryId" = t."CategoryId" AND other."SystemRole" IS NULL);

                DELETE FROM "TransactionTypes" WHERE "SystemRole" IS NOT NULL;
                DELETE FROM "Categories" WHERE "Id" IN (SELECT "CategoryId" FROM m7_7_phase5_fiado_owned);

                DROP TABLE m7_7_phase5_fiado_owned;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Transactions_GeneratedFromTransactionId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_GeneratedFromTransactionId",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_GenerationOrigin",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NotPaymentMethod",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "GeneratedFromTransactionId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "GenerationKind",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FiadoPaymentKind",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "SystemRole",
                table: "TransactionTypes");
        }
    }
}
