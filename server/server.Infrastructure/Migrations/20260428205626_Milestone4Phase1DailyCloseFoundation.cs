using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Milestone4Phase1DailyCloseFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCloses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyCloses_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloses_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloses_Operators_SubmittedByOperatorId",
                        column: x => x.SubmittedByOperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloses_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyCloseItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    DailyCloseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCloseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyCloseItems_DailyCloses_DailyCloseId",
                        column: x => x.DailyCloseId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloseItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseItems_DailyCloseId_ProductId",
                table: "DailyCloseItems",
                columns: new[] { "DailyCloseId", "ProductId" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseItems_ProductId",
                table: "DailyCloseItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_AccountId",
                table: "DailyCloses",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_ApprovedByUserId",
                table: "DailyCloses",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_BranchId_AccountId_Date",
                table: "DailyCloses",
                columns: new[] { "BranchId", "AccountId", "Date" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_BranchId_AccountId_Status",
                table: "DailyCloses",
                columns: new[] { "BranchId", "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_BranchId_Date_AccountId",
                table: "DailyCloses",
                columns: new[] { "BranchId", "Date", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_BranchId_Status",
                table: "DailyCloses",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_SubmittedByOperatorId",
                table: "DailyCloses",
                column: "SubmittedByOperatorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCloseItems");

            migrationBuilder.DropTable(
                name: "DailyCloses");
        }
    }
}
