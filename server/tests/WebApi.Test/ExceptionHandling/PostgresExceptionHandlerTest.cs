using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.ExceptionHandling;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace WebApi.Test.ExceptionHandling;

public class PostgresExceptionHandlerTest
{
    [Theory]
    [MemberData(nameof(KnownUniqueConstraintMessages))]
    public void Normalize_ShouldReturnConflictException_WhenKnownUniqueConstraintIsViolated(
        string constraintName,
        string expectedMessage)
    {
        var exception = BuildUniqueViolation(constraintName);

        var normalized = PostgresExceptionHandler.Normalize(exception);

        var conflictException = normalized.ShouldBeOfType<ConflictException>();
        conflictException.Message.ShouldBe(expectedMessage);
    }

    public static TheoryData<string, string> KnownUniqueConstraintMessages =>
        new()
        {
            { "IX_Operators_BranchId_UserId", ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED },
            { "IX_Clients_BranchId_Cpf", ResourcesErrorMessages.CLIENT_CPF_CONFLICT },
            { "IX_DailyCloses_BranchId_AccountId_Date", ResourcesErrorMessages.DAILYCLOSE_DATE_CONFLICT },
            { "IX_DailyCloseItems_DailyCloseId_ProductId", ResourcesErrorMessages.DAILYCLOSE_ITEM_DUPLICATE },
            { "IX_TimeEntries_BranchId_OperatorId_Date", ResourcesErrorMessages.TIMEENTRY_DATE_CONFLICT },
            { "IX_TimeEntrySegments_TimeEntryId_OpenShift", ResourcesErrorMessages.TIMEENTRY_OPEN_SEGMENT_CONFLICT },
            { "IX_Holidays_BranchId_Date", ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT },
            { "IX_Categories_BranchId_Name", ResourcesErrorMessages.CATEGORY_NAME_CONFLICT },
            { "IX_TransactionTypes_CategoryId_Name", ResourcesErrorMessages.TRANSACTION_TYPE_NAME_CONFLICT },
            { "IX_Products_BranchId_Name", ResourcesErrorMessages.PRODUCT_NAME_CONFLICT }
        };

    [Fact]
    public void Normalize_ShouldReturnOriginalException_WhenConstraintIsUnknown()
    {
        var exception = BuildUniqueViolation("IX_Unknown");

        var normalized = PostgresExceptionHandler.Normalize(exception);

        normalized.ShouldBeSameAs(exception);
    }

    [Fact]
    public void Normalize_ShouldReturnOriginalException_WhenSqlStateIsNotUniqueViolation()
    {
        var exception = BuildPostgresDbUpdateException(
            sqlState: "23503",
            constraintName: "IX_Operators_BranchId_UserId");

        var normalized = PostgresExceptionHandler.Normalize(exception);

        normalized.ShouldBeSameAs(exception);
    }

    [Fact]
    public void Normalize_ShouldReturnOriginalException_WhenExceptionIsNotDbUpdateException()
    {
        var exception = new InvalidOperationException("Not an EF update exception");

        var normalized = PostgresExceptionHandler.Normalize(exception);

        normalized.ShouldBeSameAs(exception);
    }

    [Fact]
    public void Normalize_ShouldReturnOriginalException_WhenInnerExceptionIsNotPostgresException()
    {
        var exception = new DbUpdateException("Update failed", new InvalidOperationException("Provider error"));

        var normalized = PostgresExceptionHandler.Normalize(exception);

        normalized.ShouldBeSameAs(exception);
    }

    [Fact]
    public void Normalize_ShouldReturnOriginalException_WhenConcurrencyExceptionHasNoDailyCloseEntry()
    {
        var exception = new DbUpdateConcurrencyException("Non-DailyClose concurrency failure");

        var normalized = PostgresExceptionHandler.Normalize(exception);

        normalized.ShouldBeSameAs(exception);
    }

    [Fact]
    public void Normalize_ShouldReturnRetryableConflict_WhenLockTimeoutIsWrappedByDbUpdateException()
    {
        var exception = BuildPostgresDbUpdateException(
            sqlState: "55P03",
            constraintName: "not_applicable");

        var normalized = PostgresExceptionHandler.Normalize(exception);

        normalized.ShouldBeOfType<ConflictException>().Message.ShouldBe(
            ResourcesErrorMessages.DAILYCLOSE_LEDGER_COORDINATION_BUSY);
    }

    private static DbUpdateException BuildUniqueViolation(string constraintName)
    {
        return BuildPostgresDbUpdateException("23505", constraintName);
    }

    private static DbUpdateException BuildPostgresDbUpdateException(string sqlState, string constraintName)
    {
        var postgresException = new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState,
            detail: null,
            hint: null,
            position: 0,
            internalPosition: 0,
            internalQuery: null,
            where: null,
            schemaName: "public",
            tableName: "Operators",
            columnName: null,
            dataTypeName: null,
            constraintName: constraintName,
            file: null,
            line: null,
            routine: null);

        return new DbUpdateException("Unique violation", postgresException);
    }
}
