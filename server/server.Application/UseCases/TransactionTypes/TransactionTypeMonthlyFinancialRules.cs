using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TransactionTypes;

internal static class TransactionTypeMonthlyFinancialRules
{
    /// <summary>
    /// M11 Phase 6.1: the bolão service fee is an inflow and a commercial leftover an outflow, so a role is accepted only
    /// on a type whose (immutable) category has that direction, and every row of the role has one direction.
    /// </summary>
    public static void EnsureDirectionMatches(MonthlyFinancialRole role, Direction categoryDirection)
    {
        var valid = role switch
        {
            MonthlyFinancialRole.BolaoServiceFee => categoryDirection == Direction.In,
            MonthlyFinancialRole.CommercialLeftover => categoryDirection == Direction.Out,
            _ => true
        };

        if (valid is false)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_TYPE_MONTHLY_FINANCIAL_ROLE_DIRECTION_INVALID);
    }
}
