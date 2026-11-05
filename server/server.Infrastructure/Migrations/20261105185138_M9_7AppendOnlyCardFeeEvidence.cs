using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7AppendOnlyCardFeeEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardSettlementFeeEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedFee = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ObservedNetAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    Basis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "date", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    FeeFinancialClass = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardSettlementFeeEvidence", x => x.Id);
                    table.CheckConstraint("CK_CardSettlementFeeEvidence_Values", "\"Sequence\" > 0 AND \"ConfirmedFee\" >= 0 AND (\"ObservedNetAmount\" IS NULL OR \"ObservedNetAmount\" > 0) AND \"FeeFinancialClass\" = 3 AND \"PostedOn\" >= \"OccurredOn\" AND length(btrim(\"Basis\")) > 0 AND length(btrim(\"Evidence\")) > 0 AND length(btrim(\"Reason\")) > 0");
                    table.ForeignKey(
                        name: "FK_CardSettlementFeeEvidence_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardSettlementFeeEvidence_CardSettlementBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "CardSettlementBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardSettlementFeeEvidence_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardSettlementFeeEvidence_BatchId_Sequence",
                table: "CardSettlementFeeEvidence",
                columns: new[] { "BatchId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardSettlementFeeEvidence_BranchId",
                table: "CardSettlementFeeEvidence",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CardSettlementFeeEvidence_CreatedByUserId",
                table: "CardSettlementFeeEvidence",
                column: "CreatedByUserId");
            migrationBuilder.Sql(CardFeeEvidenceSql.Install);
            migrationBuilder.Sql(CardFeeEvidenceSql.MatchFunction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CardFeeEvidenceSql.PreflightDown);
            migrationBuilder.Sql(CardFeeEvidenceSql.RestoreMatchFunction);
            migrationBuilder.DropTable(
                name: "CardSettlementFeeEvidence");
        }
    }
}
