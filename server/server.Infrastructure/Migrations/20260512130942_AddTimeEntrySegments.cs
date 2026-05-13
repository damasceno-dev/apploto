using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeEntrySegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimeEntrySegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClockIn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClockOut = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TimeEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeEntrySegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeEntrySegments_TimeEntries_TimeEntryId",
                        column: x => x.TimeEntryId,
                        principalTable: "TimeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimeEntrySegments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntrySegments_TimeEntryId_ClockIn",
                table: "TimeEntrySegments",
                columns: new[] { "TimeEntryId", "ClockIn" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntrySegments_TimeEntryId_OpenShift",
                table: "TimeEntrySegments",
                column: "TimeEntryId",
                unique: true,
                filter: "\"ClockOut\" IS NULL AND \"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntrySegments_UpdatedByUserId",
                table: "TimeEntrySegments",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimeEntrySegments");
        }
    }
}
