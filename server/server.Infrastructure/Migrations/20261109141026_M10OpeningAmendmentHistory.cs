using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M10OpeningAmendmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreviousId",
                table: "GoLiveOpenings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "GoLiveOpenings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "GoLiveOpenings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveOpenings_PreviousId",
                table: "GoLiveOpenings",
                column: "PreviousId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoLiveOpenings_GoLiveOpenings_PreviousId",
                table: "GoLiveOpenings",
                column: "PreviousId",
                principalTable: "GoLiveOpenings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoLiveOpenings_GoLiveOpenings_PreviousId",
                table: "GoLiveOpenings");

            migrationBuilder.DropIndex(
                name: "IX_GoLiveOpenings_PreviousId",
                table: "GoLiveOpenings");

            migrationBuilder.DropColumn(
                name: "PreviousId",
                table: "GoLiveOpenings");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "GoLiveOpenings");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "GoLiveOpenings");
        }
    }
}
