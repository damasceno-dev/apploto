using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7_7Phase76CloseReviewEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Closes that already exist were never covered by review-event capture, so their history is
            // incomplete (false). New closes are inserted with the entity default (true).
            migrationBuilder.AddColumn<bool>(
                name: "ReviewHistoryComplete",
                table: "DailyCloses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DailyCloseReviewEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyCloseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ActorOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorOperatorName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceDailyCloseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: true),
                    PreviousSourceDailyCloseId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedStatusBefore = table.Column<short>(type: "smallint", nullable: true),
                    AffectedStatusAfter = table.Column<short>(type: "smallint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCloseReviewEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyCloseReviewEvents_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloseReviewEvents_DailyCloses_DailyCloseId",
                        column: x => x.DailyCloseId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloseReviewEvents_DailyCloses_PreviousSourceDailyClose~",
                        column: x => x.PreviousSourceDailyCloseId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloseReviewEvents_DailyCloses_SourceDailyCloseId",
                        column: x => x.SourceDailyCloseId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloseReviewEvents_Operators_ActorOperatorId",
                        column: x => x.ActorOperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloseReviewEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyCloseReviewEventItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyCloseReviewEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BeforeValue = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    AfterValue = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCloseReviewEventItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyCloseReviewEventItems_DailyCloseReviewEvents_DailyClos~",
                        column: x => x.DailyCloseReviewEventId,
                        principalTable: "DailyCloseReviewEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloseReviewEventItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseReviewEventItems_EventId_ProductId",
                table: "DailyCloseReviewEventItems",
                columns: new[] { "DailyCloseReviewEventId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseReviewEventItems_ProductId",
                table: "DailyCloseReviewEventItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseReviewEvents_ActorOperatorId",
                table: "DailyCloseReviewEvents",
                column: "ActorOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseReviewEvents_ActorUserId",
                table: "DailyCloseReviewEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseReviewEvents_BranchId_DailyCloseId_OccurredAt",
                table: "DailyCloseReviewEvents",
                columns: new[] { "BranchId", "DailyCloseId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseReviewEvents_DailyCloseId",
                table: "DailyCloseReviewEvents",
                column: "DailyCloseId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseReviewEvents_PreviousSourceDailyCloseId",
                table: "DailyCloseReviewEvents",
                column: "PreviousSourceDailyCloseId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseReviewEvents_SourceDailyCloseId",
                table: "DailyCloseReviewEvents",
                column: "SourceDailyCloseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCloseReviewEventItems");

            migrationBuilder.DropTable(
                name: "DailyCloseReviewEvents");

            migrationBuilder.DropColumn(
                name: "ReviewHistoryComplete",
                table: "DailyCloses");
        }
    }
}
