using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M8Phase8LoginThrottles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoginThrottles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccountKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    WindowStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BlockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginThrottles", x => x.Id);
                    table.CheckConstraint("CK_LoginThrottles_FailureCount_Positive", "\"FailureCount\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryThrottles_NextAllowedAt",
                table: "RecoveryThrottles",
                column: "NextAllowedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoginThrottles_AccountKeyHash",
                table: "LoginThrottles",
                column: "AccountKeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_LoginThrottles_ExpiresAt",
                table: "LoginThrottles",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoginThrottles_KeyHash",
                table: "LoginThrottles",
                column: "KeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginThrottles");

            migrationBuilder.DropIndex(
                name: "IX_RecoveryThrottles_NextAllowedAt",
                table: "RecoveryThrottles");
        }
    }
}
