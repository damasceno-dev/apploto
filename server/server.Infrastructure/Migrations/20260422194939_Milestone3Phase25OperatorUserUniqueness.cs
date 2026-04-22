using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Milestone3Phase25OperatorUserUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Operators"
                        WHERE "UserId" IS NOT NULL
                          AND "Active" = true
                        GROUP BY "BranchId", "UserId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot create unique active operator user-link index. Duplicate active Operators exist for the same BranchId/UserId. Inspect with: SELECT "BranchId", "UserId", COUNT(*) FROM "Operators" WHERE "UserId" IS NOT NULL AND "Active" = true GROUP BY "BranchId", "UserId" HAVING COUNT(*) > 1;';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Operators_BranchId_UserId",
                table: "Operators",
                columns: new[] { "BranchId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL AND \"Active\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Operators_BranchId_UserId",
                table: "Operators");
        }
    }
}
