using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_8CommissionClassificationRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CommissionFeePosting_AttemptTransactionSource",
                table: "EmployeeCommissionFeePostings");

            migrationBuilder.AddColumn<int>(
                name: "ClassificationRevision",
                table: "EmployeeCommissionFeePostings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_CommissionFeePosting_AttemptTransactionSource",
                table: "EmployeeCommissionFeePostings",
                columns: new[] { "SessionId", "AttemptId", "TransactionId", "Component", "SourceDate", "ClassificationRevision" },
                unique: true);
            var guard = CommissionFeePostingSql.Up[..CommissionFeePostingSql.Up.IndexOf("CREATE TRIGGER", StringComparison.Ordinal)]
                .Replace("CREATE FUNCTION", "CREATE OR REPLACE FUNCTION", StringComparison.Ordinal)
                .Replace("OR NEW.\"Component\" NOT IN", "OR NEW.\"ClassificationRevision\" IS NULL OR NEW.\"ClassificationRevision\"<=0 OR NEW.\"ClassificationRevision\"<>t.\"ClassificationRevision\" OR NEW.\"Component\" NOT IN", StringComparison.Ordinal);
            migrationBuilder.Sql(guard);
            migrationBuilder.Sql(PeopleRecordsSql.Up[..PeopleRecordsSql.Up.IndexOf("CREATE TRIGGER", StringComparison.Ordinal)]
                .Replace("CREATE FUNCTION", "CREATE OR REPLACE FUNCTION", StringComparison.Ordinal));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"EmployeeCommissionFeePostings\" WHERE \"ClassificationRevision\" IS NOT NULL) THEN RAISE EXCEPTION 'classification publication evidence prevents downgrade'; END IF; END $$;");
            migrationBuilder.Sql(CommissionFeePostingSql.Up[..CommissionFeePostingSql.Up.IndexOf("CREATE TRIGGER", StringComparison.Ordinal)]
                .Replace("CREATE FUNCTION", "CREATE OR REPLACE FUNCTION", StringComparison.Ordinal));
            migrationBuilder.DropIndex(
                name: "UX_CommissionFeePosting_AttemptTransactionSource",
                table: "EmployeeCommissionFeePostings");

            migrationBuilder.DropColumn(
                name: "ClassificationRevision",
                table: "EmployeeCommissionFeePostings");

            migrationBuilder.CreateIndex(
                name: "UX_CommissionFeePosting_AttemptTransactionSource",
                table: "EmployeeCommissionFeePostings",
                columns: new[] { "SessionId", "AttemptId", "TransactionId", "Component", "SourceDate" },
                unique: true);
        }
    }
}
