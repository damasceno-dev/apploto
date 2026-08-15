using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7_7Phase3DailyCloseIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OpenedByUserId",
                table: "DailyCloses",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordedByOperatorId",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordedByUserId",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ItemsFirstRecordedAt",
                table: "DailyCloses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpeningRecheckRequiredAt",
                table: "DailyCloses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OpeningRecheckTriggeredByDailyCloseId",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OpeningRecheckTriggeredByUserId",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_OpenedByUserId",
                table: "DailyCloses",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_OpeningRecheckTriggeredByDailyCloseId",
                table: "DailyCloses",
                column: "OpeningRecheckTriggeredByDailyCloseId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_OpeningRecheckTriggeredByUserId",
                table: "DailyCloses",
                column: "OpeningRecheckTriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_RecordedByOperatorId",
                table: "DailyCloses",
                column: "RecordedByOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_RecordedByUserId",
                table: "DailyCloses",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_SubmittedByUserId",
                table: "DailyCloses",
                column: "SubmittedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyCloses_RecordingIdentityMatchesFirstCount",
                table: "DailyCloses",
                sql: "(\"ItemsFirstRecordedAt\" IS NULL AND \"RecordedByUserId\" IS NULL AND \"RecordedByOperatorId\" IS NULL) OR (\"ItemsFirstRecordedAt\" IS NOT NULL AND \"RecordedByUserId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_Users_OpenedByUserId",
                table: "DailyCloses",
                column: "OpenedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_DailyCloses_OpeningRecheckTriggeredByDailyClose~",
                table: "DailyCloses",
                column: "OpeningRecheckTriggeredByDailyCloseId",
                principalTable: "DailyCloses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_Users_OpeningRecheckTriggeredByUserId",
                table: "DailyCloses",
                column: "OpeningRecheckTriggeredByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_Operators_RecordedByOperatorId",
                table: "DailyCloses",
                column: "RecordedByOperatorId",
                principalTable: "Operators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_Users_RecordedByUserId",
                table: "DailyCloses",
                column: "RecordedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_Users_SubmittedByUserId",
                table: "DailyCloses",
                column: "SubmittedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_Users_OpenedByUserId",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_DailyCloses_OpeningRecheckTriggeredByDailyClose~",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_Users_OpeningRecheckTriggeredByUserId",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_Operators_RecordedByOperatorId",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_Users_RecordedByUserId",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_Users_SubmittedByUserId",
                table: "DailyCloses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyCloses_RecordingIdentityMatchesFirstCount",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_OpenedByUserId",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_OpeningRecheckTriggeredByDailyCloseId",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_OpeningRecheckTriggeredByUserId",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_RecordedByOperatorId",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_RecordedByUserId",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_SubmittedByUserId",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "OpenedByUserId",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "ItemsFirstRecordedAt",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "OpeningRecheckRequiredAt",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "OpeningRecheckTriggeredByDailyCloseId",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "OpeningRecheckTriggeredByUserId",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "RecordedByOperatorId",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "RecordedByUserId",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "DailyCloses");
        }
    }
}
