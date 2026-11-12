using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M10DedicatedOpeningObligations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GoLiveOpeningId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoLiveOpenings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCheque = table.Column<bool>(type: "boolean", nullable: false),
                    Identity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceEvidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AsOfDate = table.Column<DateTime>(type: "date", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoLiveOpenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoLiveOpenings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoLiveOpenings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoLiveOpenings_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoLiveOpenings_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_GoLiveOpeningId",
                table: "Transactions",
                column: "GoLiveOpeningId");

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveOpenings_AccountId",
                table: "GoLiveOpenings",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveOpenings_BranchId_Identity",
                table: "GoLiveOpenings",
                columns: new[] { "BranchId", "Identity" },
                unique: true,
                filter: "\"Active\"");

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveOpenings_ClientId",
                table: "GoLiveOpenings",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveOpenings_RecordedByUserId",
                table: "GoLiveOpenings",
                column: "RecordedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_GoLiveOpenings_GoLiveOpeningId",
                table: "Transactions",
                column: "GoLiveOpeningId",
                principalTable: "GoLiveOpenings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_GoLiveOpenings_GoLiveOpeningId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "GoLiveOpenings");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_GoLiveOpeningId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "GoLiveOpeningId",
                table: "Transactions");
        }
    }
}
