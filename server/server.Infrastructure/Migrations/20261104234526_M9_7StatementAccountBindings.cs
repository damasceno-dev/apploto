using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7StatementAccountBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountBindingId",
                table: "BankStatementImports",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankStatementAccountBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    BankIdentity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountIdentity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementAccountBindings", x => x.Id);
                    table.CheckConstraint("CK_BankStatementAccountBindings_Identity", "length(btrim(\"BankIdentity\")) > 0 AND length(btrim(\"AccountIdentity\")) > 0");
                    table.ForeignKey(
                        name: "FK_BankStatementAccountBindings_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementAccountBindings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementAccountBindings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_AccountBindingId",
                table: "BankStatementImports",
                column: "AccountBindingId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementAccountBindings_BankAccountId",
                table: "BankStatementAccountBindings",
                column: "BankAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementAccountBindings_BranchId",
                table: "BankStatementAccountBindings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementAccountBindings_CreatedByUserId",
                table: "BankStatementAccountBindings",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankStatementImports_BankStatementAccountBindings_AccountBi~",
                table: "BankStatementImports",
                column: "AccountBindingId",
                principalTable: "BankStatementAccountBindings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(StatementAccountBindingsSql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS (SELECT 1 FROM "BankStatementAccountBindings") THEN RAISE EXCEPTION 'M97_BANKING_DOWNGRADE: statement bindings would be lost'; END IF; END $$;
                DROP FUNCTION bank_statement_import_binding_guard() CASCADE;
                DROP FUNCTION bank_statement_account_binding_guard() CASCADE;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_BankStatementImports_BankStatementAccountBindings_AccountBi~",
                table: "BankStatementImports");

            migrationBuilder.DropTable(
                name: "BankStatementAccountBindings");

            migrationBuilder.DropIndex(
                name: "IX_BankStatementImports_AccountBindingId",
                table: "BankStatementImports");

            migrationBuilder.DropColumn(
                name: "AccountBindingId",
                table: "BankStatementImports");
        }
    }
}
