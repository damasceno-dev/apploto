using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.Services.Receivables;

/// <summary>Cheque response adapter over the shared current-authority, month/account and atomic idempotency envelope.</summary>
public class ChequeCommandRunner(BankCommandRunner runner)
{
    public Task<ResponseChequeOperationJson> Run<TRequest>(
        string endpoint,
        TRequest request,
        string? idempotencyKey,
        Func<BranchUser, CancellationToken, Task<IReadOnlyCollection<Guid>>> resolveAccountIds,
        Func<BranchUser, CancellationToken, Task<(Guid ResourceId, ResponseChequeOperationJson Response)>> execute,
        CancellationToken ct = default) => runner.Run<TRequest, ResponseChequeOperationJson>(endpoint, request, idempotencyKey, resolveAccountIds, execute, ct);
}
