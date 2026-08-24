using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7_7Phase7TimeEntryPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimeEntryPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    DailyTargetHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    LunchDeductionOver6H = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    LunchDeductionOver4H = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeEntryPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeEntryPolicies_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntryPolicies_BranchId_EffectiveFrom",
                table: "TimeEntryPolicies",
                columns: new[] { "BranchId", "EffectiveFrom" },
                unique: true,
                filter: "\"Active\" = true");

            // Fail-loud preflight: the Branch -> Setting relationship is not database-enforced
            // (Branch.Setting is nullable, and the backfill below joins through Settings), so
            // a legacy/imported/partially seeded branch with no Setting row would otherwise be
            // silently skipped by the INSERT ... SELECT below, leaving it with zero policy rows
            // and a latent failure the first time TimeEntryPolicyResolver resolves for it. Abort
            // the whole migration instead of completing with that invariant violated.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    orphan_branch_count integer;
                BEGIN
                    SELECT COUNT(*) INTO orphan_branch_count
                    FROM "Branches" b
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "Settings" s WHERE s."BranchId" = b."Id"
                    );

                    IF orphan_branch_count > 0 THEN
                        RAISE EXCEPTION
                            'M7_7Phase7TimeEntryPolicy backfill precondition failed: % branch(es) have no Setting row, so the TimeEntryPolicy backfill would leave them with zero initial policy rows. Seed a Setting row for every branch before applying this migration.',
                            orphan_branch_count;
                    END IF;
                END $$;
                """);

            // Deterministic backfill (M7.7 Phase 7 / decision 1.6a): every existing branch
            // gets exactly one initial policy row copied from its current Setting constants,
            // effective from 0001-01-01 (the DateTime.MinValue seed convention) so every
            // historical entry resolves to the constants that produced its balance. No
            // Active filter on Settings: the calculation read path never filtered it either.
            // The preflight check above guarantees this SELECT covers every branch.
            migrationBuilder.Sql(
                """
                INSERT INTO "TimeEntryPolicies" (
                    "Id",
                    "BranchId",
                    "EffectiveFrom",
                    "DailyTargetHours",
                    "LunchDeductionOver6H",
                    "LunchDeductionOver4H",
                    "CreatedAt",
                    "Active")
                SELECT
                    gen_random_uuid(),
                    s."BranchId",
                    DATE '0001-01-01',
                    s."DailyTargetHours",
                    s."LunchDeductionOver6H",
                    s."LunchDeductionOver4H",
                    now(),
                    TRUE
                FROM "Settings" s;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimeEntryPolicies");
        }
    }
}
