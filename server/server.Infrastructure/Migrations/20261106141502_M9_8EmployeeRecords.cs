using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_8EmployeeRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCommissionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "date", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    IncludeBolaoFee = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeSpecialFee = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeExplicitSources = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCommissionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionRules_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionRules_EmployeeCommissionRules_PreviousId",
                        column: x => x.PreviousId,
                        principalTable: "EmployeeCommissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionRules_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionRules_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeCommissionSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDate = table.Column<DateTime>(type: "date", nullable: false),
                    SignedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ExternalIdentity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceRole = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PreviousId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignedContribution = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCommissionSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionSources_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionSources_EmployeeCommissionSources_Previou~",
                        column: x => x.PreviousId,
                        principalTable: "EmployeeCommissionSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionSources_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionSources_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePayCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePayCategories_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayCategories_EmployeePayCategories_PreviousId",
                        column: x => x.PreviousId,
                        principalTable: "EmployeePayCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayCategories_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HourBankEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    SourceDate = table.Column<DateTime>(type: "date", nullable: false),
                    SignedHours = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    SignedContribution = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PreviousId = table.Column<Guid>(type: "uuid", nullable: true),
                    Cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalIdentity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HourBankEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HourBankEntries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HourBankEntries_HourBankEntries_PreviousId",
                        column: x => x.PreviousId,
                        principalTable: "HourBankEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HourBankEntries_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HourBankEntries_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpecialDraws",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Modality = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Contest = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "date", nullable: false),
                    Prize = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialDraws", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecialDraws_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpecialDraws_SpecialDraws_PreviousId",
                        column: x => x.PreviousId,
                        principalTable: "SpecialDraws",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpecialDraws_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePayItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMonth = table.Column<DateTime>(type: "date", nullable: false),
                    SourceDate = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    DeductionPolicy = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PreviousId = table.Column<Guid>(type: "uuid", nullable: true),
                    Cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    SignedContribution = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CommissionEvidenceJson = table.Column<string>(type: "text", nullable: true),
                    CommissionCalculationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePayItems_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayItems_EmployeePayCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "EmployeePayCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayItems_EmployeePayItems_PreviousId",
                        column: x => x.PreviousId,
                        principalTable: "EmployeePayItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayItems_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayItems_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpecialDrawAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    DrawId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionFieldEffectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRevision = table.Column<int>(type: "integer", nullable: false),
                    SourceValue = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    SourceDate = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    SignedContribution = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PreviousId = table.Column<Guid>(type: "uuid", nullable: true),
                    Cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialDrawAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecialDrawAllocations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpecialDrawAllocations_DailyCloses_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpecialDrawAllocations_SessionFieldEffects_SessionFieldEffe~",
                        column: x => x.SessionFieldEffectId,
                        principalTable: "SessionFieldEffects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpecialDrawAllocations_SpecialDrawAllocations_PreviousId",
                        column: x => x.PreviousId,
                        principalTable: "SpecialDrawAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpecialDrawAllocations_SpecialDraws_DrawId",
                        column: x => x.DrawId,
                        principalTable: "SpecialDraws",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpecialDrawAllocations_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionRules_ActorUserId",
                table: "EmployeeCommissionRules",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionRules_BranchId_PostedOn",
                table: "EmployeeCommissionRules",
                columns: new[] { "BranchId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionRules_OperatorId",
                table: "EmployeeCommissionRules",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionRules_PreviousId",
                table: "EmployeeCommissionRules",
                column: "PreviousId",
                unique: true,
                filter: "\"PreviousId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionSources_ActorUserId",
                table: "EmployeeCommissionSources",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionSources_BranchId_ExternalIdentity",
                table: "EmployeeCommissionSources",
                columns: new[] { "BranchId", "ExternalIdentity" },
                unique: true,
                filter: "\"PreviousId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionSources_BranchId_PostedOn",
                table: "EmployeeCommissionSources",
                columns: new[] { "BranchId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionSources_OperatorId",
                table: "EmployeeCommissionSources",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionSources_PreviousId",
                table: "EmployeeCommissionSources",
                column: "PreviousId",
                unique: true,
                filter: "\"PreviousId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayCategories_ActorUserId",
                table: "EmployeePayCategories",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayCategories_BranchId_PostedOn",
                table: "EmployeePayCategories",
                columns: new[] { "BranchId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayCategories_PreviousId",
                table: "EmployeePayCategories",
                column: "PreviousId",
                unique: true,
                filter: "\"PreviousId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayItems_ActorUserId",
                table: "EmployeePayItems",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayItems_BranchId_CommissionCalculationId",
                table: "EmployeePayItems",
                columns: new[] { "BranchId", "CommissionCalculationId" },
                unique: true,
                filter: "\"CommissionCalculationId\" IS NOT NULL AND \"PreviousId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayItems_BranchId_PostedOn",
                table: "EmployeePayItems",
                columns: new[] { "BranchId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayItems_CategoryId",
                table: "EmployeePayItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayItems_OperatorId",
                table: "EmployeePayItems",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayItems_PreviousId",
                table: "EmployeePayItems",
                column: "PreviousId",
                unique: true,
                filter: "\"PreviousId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HourBankEntries_ActorUserId",
                table: "HourBankEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HourBankEntries_BranchId_ExternalIdentity",
                table: "HourBankEntries",
                columns: new[] { "BranchId", "ExternalIdentity" },
                unique: true,
                filter: "\"ExternalIdentity\" <> '' AND \"PreviousId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HourBankEntries_BranchId_OperatorId",
                table: "HourBankEntries",
                columns: new[] { "BranchId", "OperatorId" },
                unique: true,
                filter: "\"Kind\" = 1 AND \"PreviousId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HourBankEntries_BranchId_PostedOn",
                table: "HourBankEntries",
                columns: new[] { "BranchId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_HourBankEntries_OperatorId",
                table: "HourBankEntries",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_HourBankEntries_PreviousId",
                table: "HourBankEntries",
                column: "PreviousId",
                unique: true,
                filter: "\"PreviousId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDrawAllocations_ActorUserId",
                table: "SpecialDrawAllocations",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDrawAllocations_BranchId_PostedOn",
                table: "SpecialDrawAllocations",
                columns: new[] { "BranchId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDrawAllocations_DrawId",
                table: "SpecialDrawAllocations",
                column: "DrawId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDrawAllocations_PreviousId",
                table: "SpecialDrawAllocations",
                column: "PreviousId",
                unique: true,
                filter: "\"PreviousId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDrawAllocations_SessionFieldEffectId",
                table: "SpecialDrawAllocations",
                column: "SessionFieldEffectId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDrawAllocations_SessionId",
                table: "SpecialDrawAllocations",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDraws_ActorUserId",
                table: "SpecialDraws",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDraws_BranchId_PostedOn",
                table: "SpecialDraws",
                columns: new[] { "BranchId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDraws_PreviousId",
                table: "SpecialDraws",
                column: "PreviousId",
                unique: true,
                filter: "\"PreviousId\" IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_EmployeePayItems_BranchId_OperatorId_SourceMonth", table: "EmployeePayItems",
                columns: new[] { "BranchId", "OperatorId", "SourceMonth" }, unique: true,
                filter: "\"CommissionCalculationId\" IS NOT NULL AND \"PreviousId\" IS NULL");
            migrationBuilder.Sql(PeopleRecordsSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "EmployeePayCategories") OR EXISTS (SELECT 1 FROM "EmployeePayItems") OR EXISTS (SELECT 1 FROM "EmployeeCommissionRules") OR EXISTS (SELECT 1 FROM "EmployeeCommissionSources") OR EXISTS (SELECT 1 FROM "HourBankEntries") OR EXISTS (SELECT 1 FROM "SpecialDraws") OR EXISTS (SELECT 1 FROM "SpecialDrawAllocations") THEN
                        RAISE EXCEPTION 'M98_DOWNGRADE: preserve people history';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "EmployeeCommissionRules");

            migrationBuilder.DropTable(
                name: "EmployeeCommissionSources");

            migrationBuilder.DropTable(
                name: "EmployeePayItems");

            migrationBuilder.DropTable(
                name: "HourBankEntries");

            migrationBuilder.DropTable(
                name: "SpecialDrawAllocations");

            migrationBuilder.DropTable(
                name: "EmployeePayCategories");

            migrationBuilder.DropTable(
                name: "SpecialDraws");
            migrationBuilder.Sql("DROP FUNCTION people_record_guard();");
        }
    }
}
