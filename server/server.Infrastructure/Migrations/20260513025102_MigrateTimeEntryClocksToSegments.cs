using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateTimeEntryClocksToSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClockIn",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "ClockOut",
                table: "TimeEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "ClockIn",
                table: "TimeEntries",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ClockOut",
                table: "TimeEntries",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "TimeEntries" te
                SET
                    "ClockIn" = first_segment."ClockIn"::time,
                    "ClockOut" = first_segment."ClockOut"::time
                FROM (
                    SELECT DISTINCT ON ("TimeEntryId")
                        "TimeEntryId",
                        "ClockIn",
                        "ClockOut"
                    FROM "TimeEntrySegments"
                    WHERE "Active" = TRUE
                    ORDER BY "TimeEntryId", "ClockIn", "CreatedAt", "Id"
                ) first_segment
                WHERE te."Id" = first_segment."TimeEntryId";
                """);
        }
    }
}
