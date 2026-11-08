using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_8CommissionFeePostings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCommissionFeePostings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDate = table.Column<DateTime>(type: "date", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "date", nullable: false),
                    Component = table.Column<short>(type: "smallint", nullable: false),
                    SignedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ClassificationProvenance = table.Column<short>(type: "smallint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCommissionFeePostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionFeePostings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionFeePostings_DailyCloses_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DailyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionFeePostings_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionFeePostings_SessionCountAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "SessionCountAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCommissionFeePostings_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionFeePostings_AttemptId",
                table: "EmployeeCommissionFeePostings",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionFeePostings_BranchId_OperatorId_PostedOn",
                table: "EmployeeCommissionFeePostings",
                columns: new[] { "BranchId", "OperatorId", "PostedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionFeePostings_OperatorId",
                table: "EmployeeCommissionFeePostings",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "UX_CommissionFeePosting_AttemptTransactionSource",
                table: "EmployeeCommissionFeePostings",
                columns: new[] { "SessionId", "AttemptId", "TransactionId", "Component", "SourceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCommissionFeePostings_TransactionId",
                table: "EmployeeCommissionFeePostings",
                column: "TransactionId");
            migrationBuilder.Sql(CommissionFeePostingSql.Up);
            migrationBuilder.Sql(PeopleRecordsSql.Up[..PeopleRecordsSql.Up.IndexOf("CREATE TRIGGER", StringComparison.Ordinal)]
                .Replace("CREATE FUNCTION", "CREATE OR REPLACE FUNCTION", StringComparison.Ordinal));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"EmployeeCommissionFeePostings\") THEN RAISE EXCEPTION 'commission posting history prevents downgrade'; END IF; END $$;");
            migrationBuilder.DropTable(name: "EmployeeCommissionFeePostings");
            migrationBuilder.Sql("DROP FUNCTION commission_fee_posting_guard();");
        }
    }
}
