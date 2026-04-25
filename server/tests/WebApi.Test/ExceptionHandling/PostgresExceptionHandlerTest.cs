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
            { "IX_Clients_BranchId_Cpf", ResourcesErrorMessages.CLIENT_CPF_CONFLICT }
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
