using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7PersonalResponsibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PersonalShortageThreshold",
                table: "Settings",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OpeningDiscrepancyAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscrepancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComparisonRevision = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    PreviousAdjustmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PriorAttributionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredecessorSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    HandoverId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceDate = table.Column<DateTime>(type: "date", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Target = table.Column<short>(type: "smallint", nullable: false),
                    PreviousOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousSignedAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignedAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningDiscrepancyAdjustments", x => x.Id);
                    table.CheckConstraint("CK_OpeningDiscrepancyAdjustments_Neutral", "(\"OperatorId\" IS NOT NULL OR \"SignedAmount\" = 0) AND (\"PreviousOperatorId\" IS NOT NULL OR \"PreviousSignedAmount\" = 0) AND (\"Target\" <> 3 OR (\"OperatorId\" IS NULL AND \"SignedAmount\" = 0))");
                    table.CheckConstraint("CK_OpeningDiscrepancyAdjustments_Sequence", "\"Sequence\" > 0 AND \"ComparisonRevision\" > 0");
                    table.CheckConstraint("CK_OpeningDiscrepancyAdjustments_Target", "\"Target\" IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAdjustments_DailyCloses_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAdjustments_OpeningDiscrepancyAdjustments~",
                        column: x => x.PreviousAdjustmentId,
                        principalTable: "OpeningDiscrepancyAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAdjustments_OpeningDiscrepancyAttribution~",
                        column: x => x.PriorAttributionId,
                        principalTable: "OpeningDiscrepancyAttributionEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAdjustments_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAdjustments_Operators_PreviousOperatorId",
                        column: x => x.PreviousOperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAdjustments_SessionOpeningDiscrepancies_D~",
                        column: x => x.DiscrepancyId,
                        principalTable: "SessionOpeningDiscrepancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAdjustments_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Settings_PersonalShortageThreshold",
                table: "Settings",
                sql: "\"PersonalShortageThreshold\" IS NULL OR \"PersonalShortageThreshold\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAdjustments_ActorUserId",
                table: "OpeningDiscrepancyAdjustments",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAdjustments_BranchId_PostedOn",
                table: "OpeningDiscrepancyAdjustments",
                columns: new[] { "BranchId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAdjustments_DiscrepancyId_Sequence",
                table: "OpeningDiscrepancyAdjustments",
                columns: new[] { "DiscrepancyId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAdjustments_OperatorId",
                table: "OpeningDiscrepancyAdjustments",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAdjustments_PreviousAdjustmentId",
                table: "OpeningDiscrepancyAdjustments",
                column: "PreviousAdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAdjustments_PreviousOperatorId",
                table: "OpeningDiscrepancyAdjustments",
                column: "PreviousOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAdjustments_PriorAttributionId",
                table: "OpeningDiscrepancyAdjustments",
                column: "PriorAttributionId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAdjustments_SessionId",
                table: "OpeningDiscrepancyAdjustments",
                column: "SessionId");
            migrationBuilder.Sql(PersonalResponsibilitySql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PersonalResponsibilitySql.Down);
            migrationBuilder.DropTable(
                name: "OpeningDiscrepancyAdjustments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Settings_PersonalShortageThreshold",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PersonalShortageThreshold",
                table: "Settings");
        }
    }
}
