using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M8OperatedOnboardingAndGoogleIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformOperator",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GoogleIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleIdentities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoogleLoginChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NonceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleLoginChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperatedOnboardings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerInvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatedOnboardings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatedOnboardings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatedOnboardings_Invitations_OwnerInvitationId",
                        column: x => x.OwnerInvitationId,
                        principalTable: "Invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatedOnboardings_Users_PlatformUserId",
                        column: x => x.PlatformUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleIdentities_Issuer_Subject",
                table: "GoogleIdentities",
                columns: new[] { "Issuer", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleIdentities_UserId",
                table: "GoogleIdentities",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleLoginChallenges_NonceHash",
                table: "GoogleLoginChallenges",
                column: "NonceHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleLoginChallenges_StateHash",
                table: "GoogleLoginChallenges",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatedOnboardings_BranchId",
                table: "OperatedOnboardings",
                column: "BranchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatedOnboardings_OwnerInvitationId",
                table: "OperatedOnboardings",
                column: "OwnerInvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatedOnboardings_PlatformUserId",
                table: "OperatedOnboardings",
                column: "PlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatedOnboardings_RequestId",
                table: "OperatedOnboardings",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleIdentities");

            migrationBuilder.DropTable(
                name: "GoogleLoginChallenges");

            migrationBuilder.DropTable(
                name: "OperatedOnboardings");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsPlatformOperator",
                table: "Users");
        }
    }
}
