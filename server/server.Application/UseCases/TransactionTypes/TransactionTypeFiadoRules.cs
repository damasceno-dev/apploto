using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TransactionTypes;

internal static class TransactionTypeFiadoRules
{
    /// <summary>
    /// A fiado repayment method (M7.7 Phase 5.1) is an <c>In</c> type that requires a Tab account and a client; the
    /// kind is configuration, never inferred from the type's name or category.
    /// </summary>
    public static void EnsureValidPaymentKind(
        FiadoPaymentKind? kind,
        Direction categoryDirection,
        bool requiresTabAccountAndClient)
    {
        if (kind is not null && (categoryDirection != Direction.In || requiresTabAccountAndClient is false))
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_TYPE_FIADO_PAYMENT_KIND_INVALID);
    }
}
