using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M10ImportAuditAndTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoLiveImportAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoLiveImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingRevision = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoLiveImportAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoLiveImportAudits_GoLiveImports_GoLiveImportId",
                        column: x => x.GoLiveImportId,
                        principalTable: "GoLiveImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoLiveImportAudits_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoLiveImportTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoLiveImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTable = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoLiveImportTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoLiveImportTargets_GoLiveImports_GoLiveImportId",
                        column: x => x.GoLiveImportId,
                        principalTable: "GoLiveImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoLiveImportTargets_Transactions_TargetId",
                        column: x => x.TargetId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveImports_BranchId",
                table: "GoLiveImports",
                column: "BranchId",
                unique: true,
                filter: "\"Status\" = 'Activated'");

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveImportAudits_ActorUserId",
                table: "GoLiveImportAudits",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveImportAudits_GoLiveImportId",
                table: "GoLiveImportAudits",
                column: "GoLiveImportId");

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveImportTargets_BranchId_SourceTable_SourceId",
                table: "GoLiveImportTargets",
                columns: new[] { "BranchId", "SourceTable", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveImportTargets_GoLiveImportId",
                table: "GoLiveImportTargets",
                column: "GoLiveImportId");

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveImportTargets_TargetId",
                table: "GoLiveImportTargets",
                column: "TargetId");
            migrationBuilder.Sql("""
                CREATE FUNCTION go_live_audit_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'go-live import evidence is append-only' USING ERRCODE = 'check_violation';
                END $$;
                CREATE TRIGGER "TR_GoLiveImportAudits_AppendOnly" BEFORE UPDATE OR DELETE ON "GoLiveImportAudits"
                    FOR EACH ROW EXECUTE FUNCTION go_live_audit_append_only();
                CREATE TRIGGER "TR_GoLiveImportTargets_AppendOnly" BEFORE UPDATE OR DELETE ON "GoLiveImportTargets"
                    FOR EACH ROW EXECUTE FUNCTION go_live_audit_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "TR_GoLiveImportAudits_AppendOnly" ON "GoLiveImportAudits";
                DROP TRIGGER "TR_GoLiveImportTargets_AppendOnly" ON "GoLiveImportTargets";
                DROP FUNCTION go_live_audit_append_only();
                """);
            migrationBuilder.DropTable(
                name: "GoLiveImportAudits");

            migrationBuilder.DropTable(
                name: "GoLiveImportTargets");

            migrationBuilder.DropIndex(
                name: "IX_GoLiveImports_BranchId",
                table: "GoLiveImports");
        }
    }
}
