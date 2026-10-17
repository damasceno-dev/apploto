using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7_7Phase5StalePairingCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // M7.7 Phase 5.3 (gap S): a deactivated account never keeps a Terminal↔Tab pairing.
            // Before this phase, deactivation left the pairing in place, so an inactive Terminal
            // could keep reserving its Tab (blocking the filtered unique TabAccountId index) and an
            // active Terminal could point at an inactive Tab. Clear both shapes; valid pairs between
            // two active accounts are untouched. Data-only: the schema is unchanged.
            migrationBuilder.Sql(@"
                UPDATE ""Accounts"" AS terminal
                SET ""TabAccountId"" = NULL
                WHERE terminal.""TabAccountId"" IS NOT NULL
                  AND (
                      terminal.""Active"" = FALSE
                      OR EXISTS (
                          SELECT 1
                          FROM ""Accounts"" AS tab
                          WHERE tab.""Id"" = terminal.""TabAccountId""
                            AND tab.""Active"" = FALSE));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design: a cleared stale pairing pointed at or from an inactive
            // account and has no valid state to restore.
        }
    }
}
