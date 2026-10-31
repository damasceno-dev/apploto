using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_6Phase4BlindCountApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OfferedAttemptId",
                table: "SessionHandovers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttemptId",
                table: "SessionEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentConfirmationToken",
                table: "DailyCloses",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<int>(
                name: "LastAttemptNumber",
                table: "DailyCloses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "OpeningRevision",
                table: "DailyCloses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "RecountStartedForAttemptId",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedAttemptId",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SessionCountAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    ConfirmationToken = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    CountRevision = table.Column<long>(type: "bigint", nullable: false),
                    LedgerRevision = table.Column<long>(type: "bigint", nullable: false),
                    OpeningRevision = table.Column<long>(type: "bigint", nullable: false),
                    TotalOpening = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalIn = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalOut = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ExpectedClosing = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalClosing = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ConferredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConferredByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ConferredByOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommandKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCountAttempts", x => x.Id);
                    table.CheckConstraint("CK_SessionCountAttempts_DifferenceMatches", "\"Difference\" = \"TotalClosing\" - \"ExpectedClosing\" AND \"ExpectedClosing\" = \"TotalOpening\" + \"TotalIn\" - \"TotalOut\"");
                    table.CheckConstraint("CK_SessionCountAttempts_NumberPositive", "\"AttemptNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_SessionCountAttempts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionCountAttempts_DailyCloses_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionCountAttempts_Operators_ConferredByOperatorId",
                        column: x => x.ConferredByOperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionCountAttempts_Users_ConferredByUserId",
                        column: x => x.ConferredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionCountAttemptItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningValue = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    CountedValue = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCountAttemptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionCountAttemptItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionCountAttemptItems_SessionCountAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "SessionCountAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionHandovers_OfferedAttemptId",
                table: "SessionHandovers",
                column: "OfferedAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_AttemptId_CountConferred",
                table: "SessionEvents",
                column: "AttemptId",
                unique: true,
                filter: "\"Kind\" = 13");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_RecountStartedForAttemptId",
                table: "DailyCloses",
                column: "RecountStartedForAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_SubmittedAttemptId",
                table: "DailyCloses",
                column: "SubmittedAttemptId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyCloses_RevisionsNonNegative",
                table: "DailyCloses",
                sql: "\"LedgerRevision\" >= 0 AND \"CountRevision\" >= 0 AND \"OpeningRevision\" >= 0 AND \"LastAttemptNumber\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCountAttemptItems_AttemptId_ProductId",
                table: "SessionCountAttemptItems",
                columns: new[] { "AttemptId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionCountAttemptItems_ProductId",
                table: "SessionCountAttemptItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCountAttempts_BranchId",
                table: "SessionCountAttempts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCountAttempts_ConferredByOperatorId",
                table: "SessionCountAttempts",
                column: "ConferredByOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCountAttempts_ConferredByUserId",
                table: "SessionCountAttempts",
                column: "ConferredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCountAttempts_SessionId_AttemptNumber",
                table: "SessionCountAttempts",
                columns: new[] { "SessionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionCountAttempts_SessionId_ConfirmationToken",
                table: "SessionCountAttempts",
                columns: new[] { "SessionId", "ConfirmationToken" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_SessionCountAttempts_RecountStartedForAttemptId",
                table: "DailyCloses",
                column: "RecountStartedForAttemptId",
                principalTable: "SessionCountAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_SessionCountAttempts_SubmittedAttemptId",
                table: "DailyCloses",
                column: "SubmittedAttemptId",
                principalTable: "SessionCountAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SessionEvents_SessionCountAttempts_AttemptId",
                table: "SessionEvents",
                column: "AttemptId",
                principalTable: "SessionCountAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SessionHandovers_SessionCountAttempts_OfferedAttemptId",
                table: "SessionHandovers",
                column: "OfferedAttemptId",
                principalTable: "SessionCountAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Existing sessions received a unique token from the column default above, once, without a count event;
            // reads never allocate one. Every revision starts at zero: no attempt exists before this phase.
            migrationBuilder.Sql(SessionCountAttemptGuardsSql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SessionCountAttemptGuardsSql.Uninstall);

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_SessionCountAttempts_RecountStartedForAttemptId",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_SessionCountAttempts_SubmittedAttemptId",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionEvents_SessionCountAttempts_AttemptId",
                table: "SessionEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionHandovers_SessionCountAttempts_OfferedAttemptId",
                table: "SessionHandovers");

            migrationBuilder.DropTable(
                name: "SessionCountAttemptItems");

            migrationBuilder.DropTable(
                name: "SessionCountAttempts");

            migrationBuilder.DropIndex(
                name: "IX_SessionHandovers_OfferedAttemptId",
                table: "SessionHandovers");

            migrationBuilder.DropIndex(
                name: "IX_SessionEvents_AttemptId_CountConferred",
                table: "SessionEvents");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_RecountStartedForAttemptId",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_SubmittedAttemptId",
                table: "DailyCloses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyCloses_RevisionsNonNegative",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "OfferedAttemptId",
                table: "SessionHandovers");

            migrationBuilder.DropColumn(
                name: "AttemptId",
                table: "SessionEvents");

            migrationBuilder.DropColumn(
                name: "CurrentConfirmationToken",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "LastAttemptNumber",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "OpeningRevision",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "RecountStartedForAttemptId",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "SubmittedAttemptId",
                table: "DailyCloses");
        }
    }
}
