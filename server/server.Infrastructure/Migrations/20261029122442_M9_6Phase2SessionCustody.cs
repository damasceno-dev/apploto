using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_6Phase2SessionCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_BranchId_AccountId_Date",
                table: "DailyCloses");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CountRevision",
                table: "DailyCloses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "CustodianOperatorId",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustodianUserIdAtStart",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CustodyHistoryComplete",
                table: "DailyCloses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "CustodyState",
                table: "DailyCloses",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "DailyCloses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LedgerRevision",
                table: "DailyCloses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<short>(
                name: "MappingState",
                table: "DailyCloses",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "Origin",
                table: "DailyCloses",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "DailyCloses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "DailyCloses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerminalId",
                table: "DailyCloses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustodyHistoryRevision",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "TerminalId",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustodyMappingIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<short>(type: "smallint", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueCode = table.Column<short>(type: "smallint", nullable: false),
                    SourceDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ResolutionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustodyMappingIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustodyMappingIssues_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningCounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOfDate = table.Column<DateTime>(type: "date", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningCounts_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningCounts_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RelatedId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionEvents_DailyCloses_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionHandovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutgoingSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomingSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IntendedIncomingOperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferedCountRevision = table.Column<long>(type: "bigint", nullable: false),
                    OfferedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<short>(type: "smallint", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionHandovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionHandovers_DailyCloses_IncomingSessionId",
                        column: x => x.IncomingSessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionHandovers_DailyCloses_OutgoingSessionId",
                        column: x => x.OutgoingSessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionHandovers_Operators_IntendedIncomingOperatorId",
                        column: x => x.IntendedIncomingOperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionHandovers_Users_OfferedByUserId",
                        column: x => x.OfferedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Terminals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terminals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Terminals_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningCountItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningCountItems", x => x.Id);
                    table.CheckConstraint("CK_OpeningCountItems_NonNegative", "\"Value\" >= 0");
                    table.ForeignKey(
                        name: "FK_OpeningCountItems_OpeningCounts_OpeningCountId",
                        column: x => x.OpeningCountId,
                        principalTable: "OpeningCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningCountItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionHandoverItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionHandoverId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionHandoverItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionHandoverItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionHandoverItems_SessionHandovers_SessionHandoverId",
                        column: x => x.SessionHandoverId,
                        principalTable: "SessionHandovers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionOpenings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceKind = table.Column<short>(type: "smallint", nullable: false),
                    SourceKind = table.Column<short>(type: "smallint", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRevision = table.Column<long>(type: "bigint", nullable: true),
                    HandoverId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcknowledgedByOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionOpenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionOpenings_DailyCloses_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionOpenings_Operators_AcknowledgedByOperatorId",
                        column: x => x.AcknowledgedByOperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionOpenings_SessionHandovers_HandoverId",
                        column: x => x.HandoverId,
                        principalTable: "SessionHandovers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionOpenings_Users_AcknowledgedByUserId",
                        column: x => x.AcknowledgedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionOpeningDiscrepancies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionOpeningId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandoverId = table.Column<Guid>(type: "uuid", nullable: true),
                    PredecessorSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceKind = table.Column<short>(type: "smallint", nullable: false),
                    CurrentRevision = table.Column<int>(type: "integer", nullable: false),
                    CurrentTotalDelta = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Attribution = table.Column<short>(type: "smallint", nullable: false),
                    AttributedRevision = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionOpeningDiscrepancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionOpeningDiscrepancies_DailyCloses_PredecessorSessionId",
                        column: x => x.PredecessorSessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionOpeningDiscrepancies_DailyCloses_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionOpeningDiscrepancies_SessionHandovers_HandoverId",
                        column: x => x.HandoverId,
                        principalTable: "SessionHandovers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionOpeningDiscrepancies_SessionOpenings_SessionOpeningId",
                        column: x => x.SessionOpeningId,
                        principalTable: "SessionOpenings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionOpeningItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionOpeningId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedValue = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    AcknowledgedValue = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionOpeningItems", x => x.Id);
                    table.CheckConstraint("CK_SessionOpeningItems_NonNegative", "\"AcknowledgedValue\" >= 0 AND (\"ProposedValue\" IS NULL OR \"ProposedValue\" >= 0)");
                    table.ForeignKey(
                        name: "FK_SessionOpeningItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionOpeningItems_SessionOpenings_SessionOpeningId",
                        column: x => x.SessionOpeningId,
                        principalTable: "SessionOpenings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningDiscrepancyAttributionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscrepancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscrepancyRevision = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<short>(type: "smallint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupersedesEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningDiscrepancyAttributionEvents", x => x.Id);
                    table.CheckConstraint("CK_OpeningDiscrepancyAttributionEvents_Target", "\"Target\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAttributionEvents_OpeningDiscrepancyAttri~",
                        column: x => x.SupersedesEventId,
                        principalTable: "OpeningDiscrepancyAttributionEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAttributionEvents_SessionOpeningDiscrepan~",
                        column: x => x.DiscrepancyId,
                        principalTable: "SessionOpeningDiscrepancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningDiscrepancyAttributionEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionOpeningDiscrepancyRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscrepancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRevision = table.Column<long>(type: "bigint", nullable: true),
                    ProposedTotal = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    AcknowledgedTotal = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalDelta = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionOpeningDiscrepancyRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionOpeningDiscrepancyRevisions_SessionOpeningDiscrepanc~",
                        column: x => x.DiscrepancyId,
                        principalTable: "SessionOpeningDiscrepancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionOpeningDiscrepancyRevisions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionOpeningDiscrepancyRevisionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedValue = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    AcknowledgedValue = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Delta = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionOpeningDiscrepancyRevisionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionOpeningDiscrepancyRevisionItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionOpeningDiscrepancyRevisionItems_SessionOpeningDiscre~",
                        column: x => x.RevisionId,
                        principalTable: "SessionOpeningDiscrepancyRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Legacy account/day closes become Unresolved history (§6.22): their custodian, sequence and interval are
            // unknown, so nothing is invented. Each legacy close, and each cashbox row without a session, gets a
            // CustodyMappingIssue for the resolution report. Development/staging fixtures may instead be reset.
            migrationBuilder.Sql(
                """
                UPDATE "DailyCloses"
                SET "MappingState" = 1, "Origin" = 4, "CustodyHistoryComplete" = false;

                INSERT INTO "CustodyMappingIssues"
                    ("Id", "BranchId", "SourceKind", "SourceId", "IssueCode", "SourceDigest", "Status", "CreatedAt", "Active")
                SELECT gen_random_uuid(), d."BranchId", 0, d."Id", 0,
                       encode(sha256(convert_to(concat_ws('|', d."Id", d."AccountId", d."Date", d."Status"), 'UTF8')), 'hex'),
                       0, now(), true
                FROM "DailyCloses" d;

                INSERT INTO "CustodyMappingIssues"
                    ("Id", "BranchId", "SourceKind", "SourceId", "IssueCode", "SourceDigest", "Status", "CreatedAt", "Active")
                SELECT gen_random_uuid(), t."BranchId", 1, t."Id", 1,
                       encode(sha256(convert_to(concat_ws('|', t."Id", t."AccountId", t."Date", t."Value", t."Status"), 'UTF8')), 'hex'),
                       0, now(), true
                FROM "Transactions" t
                JOIN "Accounts" a ON a."Id" = t."AccountId"
                WHERE a."Type" = 0 AND t."SessionId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SessionId_Status",
                table: "Transactions",
                columns: new[] { "SessionId", "Status" },
                filter: "\"SessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_BranchId_AccountId_ActiveCustody",
                table: "DailyCloses",
                columns: new[] { "BranchId", "AccountId" },
                unique: true,
                filter: "\"Active\" = true AND \"MappingState\" = 0 AND \"CustodyState\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_BranchId_AccountId_Sequence",
                table: "DailyCloses",
                columns: new[] { "BranchId", "AccountId", "Sequence" },
                unique: true,
                filter: "\"MappingState\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_CustodianOperatorId",
                table: "DailyCloses",
                column: "CustodianOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_TerminalId",
                table: "DailyCloses",
                column: "TerminalId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyCloses_CustodyStateMatchesEnd",
                table: "DailyCloses",
                sql: "\"CustodyState\" IS NULL OR ((\"CustodyState\" = 0) = (\"EndedAt\" IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyCloses_IntervalOrdered",
                table: "DailyCloses",
                sql: "\"EndedAt\" IS NULL OR \"StartedAt\" IS NULL OR \"EndedAt\" > \"StartedAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyCloses_ReadySessionIdentity",
                table: "DailyCloses",
                sql: "\"MappingState\" <> 0 OR (\"CustodyState\" IS NOT NULL AND \"Sequence\" IS NOT NULL AND \"StartedAt\" IS NOT NULL AND \"CustodianOperatorId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyCloses_SequencePositive",
                table: "DailyCloses",
                sql: "\"Sequence\" IS NULL OR \"Sequence\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TerminalId",
                table: "Accounts",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyMappingIssues_BranchId_Status",
                table: "CustodyMappingIssues",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustodyMappingIssues_SourceKind_SourceId_IssueCode",
                table: "CustodyMappingIssues",
                columns: new[] { "SourceKind", "SourceId", "IssueCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningCountItems_OpeningCountId_ProductId",
                table: "OpeningCountItems",
                columns: new[] { "OpeningCountId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningCountItems_ProductId",
                table: "OpeningCountItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningCounts_AccountId_Revision",
                table: "OpeningCounts",
                columns: new[] { "AccountId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningCounts_BranchId_AccountId_Current",
                table: "OpeningCounts",
                columns: new[] { "BranchId", "AccountId" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningCounts_RecordedByUserId",
                table: "OpeningCounts",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAttributionEvents_ActorUserId",
                table: "OpeningDiscrepancyAttributionEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAttributionEvents_DiscrepancyId_CreatedAt",
                table: "OpeningDiscrepancyAttributionEvents",
                columns: new[] { "DiscrepancyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningDiscrepancyAttributionEvents_SupersedesEventId",
                table: "OpeningDiscrepancyAttributionEvents",
                column: "SupersedesEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_ActorUserId",
                table: "SessionEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_SessionId_CreatedAt",
                table: "SessionEvents",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionHandoverItems_ProductId",
                table: "SessionHandoverItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionHandoverItems_SessionHandoverId_ProductId",
                table: "SessionHandoverItems",
                columns: new[] { "SessionHandoverId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionHandovers_IncomingSessionId",
                table: "SessionHandovers",
                column: "IncomingSessionId",
                unique: true,
                filter: "\"IncomingSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SessionHandovers_IntendedIncomingOperatorId",
                table: "SessionHandovers",
                column: "IntendedIncomingOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionHandovers_OfferedByUserId",
                table: "SessionHandovers",
                column: "OfferedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionHandovers_OutgoingSessionId_Unresolved",
                table: "SessionHandovers",
                column: "OutgoingSessionId",
                unique: true,
                filter: "\"State\" IN (0, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningDiscrepancies_BranchId_Attribution",
                table: "SessionOpeningDiscrepancies",
                columns: new[] { "BranchId", "Attribution" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningDiscrepancies_HandoverId",
                table: "SessionOpeningDiscrepancies",
                column: "HandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningDiscrepancies_PredecessorSessionId",
                table: "SessionOpeningDiscrepancies",
                column: "PredecessorSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningDiscrepancies_SessionId",
                table: "SessionOpeningDiscrepancies",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningDiscrepancies_SessionOpeningId",
                table: "SessionOpeningDiscrepancies",
                column: "SessionOpeningId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningDiscrepancyRevisionItems_ProductId",
                table: "SessionOpeningDiscrepancyRevisionItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningDiscrepancyRevisionItems_RevisionId_ProductId",
                table: "SessionOpeningDiscrepancyRevisionItems",
                columns: new[] { "RevisionId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningDiscrepancyRevisions_CreatedByUserId",
                table: "SessionOpeningDiscrepancyRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningDiscrepancyRevisions_DiscrepancyId_Revision",
                table: "SessionOpeningDiscrepancyRevisions",
                columns: new[] { "DiscrepancyId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningItems_ProductId",
                table: "SessionOpeningItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpeningItems_SessionOpeningId_ProductId",
                table: "SessionOpeningItems",
                columns: new[] { "SessionOpeningId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpenings_AcknowledgedByOperatorId",
                table: "SessionOpenings",
                column: "AcknowledgedByOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpenings_AcknowledgedByUserId",
                table: "SessionOpenings",
                column: "AcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpenings_HandoverId",
                table: "SessionOpenings",
                column: "HandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOpenings_SessionId",
                table: "SessionOpenings",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Terminals_BranchId_ExternalReference",
                table: "Terminals",
                columns: new[] { "BranchId", "ExternalReference" },
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Terminals_TerminalId",
                table: "Accounts",
                column: "TerminalId",
                principalTable: "Terminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_Operators_CustodianOperatorId",
                table: "DailyCloses",
                column: "CustodianOperatorId",
                principalTable: "Operators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_Terminals_TerminalId",
                table: "DailyCloses",
                column: "TerminalId",
                principalTable: "Terminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_DailyCloses_SessionId",
                table: "Transactions",
                column: "SessionId",
                principalTable: "DailyCloses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(SessionCustodyGuardsSql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SessionCustodyGuardsSql.Uninstall);

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Terminals_TerminalId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_Operators_CustodianOperatorId",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_Terminals_TerminalId",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_DailyCloses_SessionId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "CustodyMappingIssues");

            migrationBuilder.DropTable(
                name: "OpeningCountItems");

            migrationBuilder.DropTable(
                name: "OpeningDiscrepancyAttributionEvents");

            migrationBuilder.DropTable(
                name: "SessionEvents");

            migrationBuilder.DropTable(
                name: "SessionHandoverItems");

            migrationBuilder.DropTable(
                name: "SessionOpeningDiscrepancyRevisionItems");

            migrationBuilder.DropTable(
                name: "SessionOpeningItems");

            migrationBuilder.DropTable(
                name: "Terminals");

            migrationBuilder.DropTable(
                name: "OpeningCounts");

            migrationBuilder.DropTable(
                name: "SessionOpeningDiscrepancyRevisions");

            migrationBuilder.DropTable(
                name: "SessionOpeningDiscrepancies");

            migrationBuilder.DropTable(
                name: "SessionOpenings");

            migrationBuilder.DropTable(
                name: "SessionHandovers");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_SessionId_Status",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_BranchId_AccountId_ActiveCustody",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_BranchId_AccountId_Sequence",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_CustodianOperatorId",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_DailyCloses_TerminalId",
                table: "DailyCloses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyCloses_CustodyStateMatchesEnd",
                table: "DailyCloses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyCloses_IntervalOrdered",
                table: "DailyCloses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyCloses_ReadySessionIdentity",
                table: "DailyCloses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyCloses_SequencePositive",
                table: "DailyCloses");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_TerminalId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CountRevision",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "CustodianOperatorId",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "CustodianUserIdAtStart",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "CustodyHistoryComplete",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "CustodyState",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "LedgerRevision",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "MappingState",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "CustodyHistoryRevision",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_BranchId_AccountId_Date",
                table: "DailyCloses",
                columns: new[] { "BranchId", "AccountId", "Date" },
                unique: true,
                filter: "\"Active\" = true");
        }
    }
}
