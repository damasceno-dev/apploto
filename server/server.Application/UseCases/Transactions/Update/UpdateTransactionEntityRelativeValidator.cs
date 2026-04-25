using server.Communication.Requests;
using server.Domain.Entities;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Update;

/// <summary>
/// Update validation rules that depend on the loaded transaction. Kept out of
/// FluentValidation because they require entity state, not just request shape.
/// </summary>
internal static class UpdateTransactionEntityRelativeValidator
{
    public static void EnsureValid(Transaction transaction, RequestUpdateTransactionJson request)
    {
        if (request.DueDate < transaction.Date)
        {
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE]);
        }

        if (request.PaidAt is { } paidAt && paidAt < transaction.Date)
        {
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_PAID_AT_BEFORE_DATE]);
        }
    }
}
