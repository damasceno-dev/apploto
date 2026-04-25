using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Transactions;

public class TransactionRecordedByOperatorResolver
{
    /// <summary>
    /// RecordedByOperatorResolver
    /// Every transaction needs a RecordedByOperatorId — "which operator's terminal/context does this transaction belong to?" The rules differ by role:
    /// Member (the operator themselves): 
    /// - A Member is the operator. They sit at a terminal and record transactions;
    /// - The system always resolves their operator automatically from their user account(GetActiveByUserIdAndBranchId);
    /// - If they try to send RecordedByOperatorId in the request (any value, even their own), it's rejected — because Members shouldn't be choosing which operator context to act as. It's always "you;";
    /// - If they have no linked operator at all → also rejected (they can't record transactions).
    /// Manager/Admin (supervisory roles):
    /// - A Manager might want to record a transaction on behalf of another operator — e.g., correcting a record for an operator who already went home;
    /// - So they can supply a RecordedByOperatorId to override. If they supply one, it's used;
    /// - If they don't supply one, the system defaults to their own linked operator (if they have one);
    /// - If they have no operator link AND didn't supply one → error (the system can't figure out who the transaction belongs to).
    /// In short: Member: "You are always you. Don't tell me who you are." Manager: "You can be yourself, or you can act on behalf of someone else. Your choice."
    /// </summary>
    /// <param name="requestedOperatorId">Provided OperatorId from request</param>
    /// <param name="role">role</param>
    /// <param name="callerOperator">Detected OperatorId from UserId and Operators repository</param>
    /// <returns>OperatorId</returns>
    /// <exception cref="OnValidationException">exception</exception>
    public Guid Resolve(Guid? requestedOperatorId, Role role, Operator? callerOperator)
    {
        if (role == Role.Member)
        {
            if (requestedOperatorId is not null)
            {
                throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_MEMBER_CANNOT_OVERRIDE_RECORDED_BY_OPERATOR]);
            }

            return callerOperator?.Id
                   ?? throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK]);
        }

        if (requestedOperatorId is { } supplied)
        {
            return supplied;
        }

        return callerOperator?.Id
               ?? throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_REQUIRES_RECORDED_BY_OPERATOR]);
    }
}
