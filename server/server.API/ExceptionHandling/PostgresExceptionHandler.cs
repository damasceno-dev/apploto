using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.ExceptionHandling;

/// <summary>
/// PostgresSQL-specific exception normalization for the API layer.
/// Keeps provider details out of <see cref="ApiExceptionHandler"/> by translating
/// known database constraint violations into domain-level <see cref="ServerException"/>s.
/// </summary>
internal static class PostgresExceptionHandler
{
    private const string PostgresUniqueViolationSqlState = "23505";

    private static readonly Dictionary<string, string> UniqueConstraintMessages =
        new(StringComparer.Ordinal)
        {
            ["IX_Clients_BranchId_Cpf"] = ResourcesErrorMessages.CLIENT_CPF_CONFLICT,
            ["IX_Operators_BranchId_UserId"] = ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED,
            ["IX_DailyCloses_BranchId_AccountId_Date"] = ResourcesErrorMessages.DAILYCLOSE_DATE_CONFLICT,
            ["IX_DailyCloseItems_DailyCloseId_ProductId"] = ResourcesErrorMessages.DAILYCLOSE_ITEM_DUPLICATE,
            ["IX_TimeEntries_BranchId_OperatorId_Date"] = ResourcesErrorMessages.TIMEENTRY_DATE_CONFLICT,
            ["IX_TimeEntrySegments_TimeEntryId_OpenShift"] = ResourcesErrorMessages.TIMEENTRY_OPEN_SEGMENT_CONFLICT,
            ["IX_Holidays_BranchId_Date"] = ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT
        };

    public static Exception Normalize(Exception exception)
    {
        // Only rewrite known Postgres unique-violation cases. Everything else
        // flows through unchanged so the generic API exception handler can decide
        if (exception is ServerException || exception is not DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresUniqueViolationSqlState,
                    ConstraintName: not null
                } postgresException
            })
        {
            return exception;
        }

        return UniqueConstraintMessages.TryGetValue(postgresException.ConstraintName, out var conflictMessage)
            ? new ConflictException(conflictMessage)
            : exception;
    }
}
