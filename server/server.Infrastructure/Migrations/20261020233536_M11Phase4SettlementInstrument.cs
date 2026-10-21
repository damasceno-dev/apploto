using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M11Phase4SettlementInstrument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // M11 Phase 4: every existing type stays None (0). Classification is explicit admin configuration and is
            // never inferred from a type's name, category, direction or settlement rule.
            migrationBuilder.AddColumn<short>(
                name: "SettlementInstrument",
                table: "TransactionTypes",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NoSettlementInstrument",
                table: "TransactionTypes",
                sql: "\"SystemRole\" IS NULL OR \"SettlementInstrument\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TransactionTypes_SystemRole_NoSettlementInstrument",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "SettlementInstrument",
                table: "TransactionTypes");
        }
    }
}
