using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IBankStatementsRepository
{
    Task LockAccountBindingIdentity(Guid branchId, string bankIdentity, string accountIdentity, CancellationToken ct);
    Task<bool> AccountBindingIdentityExists(Guid branchId, string bankIdentity, string accountIdentity, CancellationToken ct);
    Task AddAccountBinding(BankStatementAccountBinding binding, CancellationToken ct);
    Task<BankStatementAccountBinding?> GetAccountBindingAsNoTracking(Guid id, Guid branchId, CancellationToken ct);
    Task<IReadOnlyList<BankStatementAccountBinding>> ListAccountBindingsAsNoTracking(Guid branchId, Guid bankAccountId, CancellationToken ct);
    Task<BankStatementImport?> FindImportAsNoTracking(Guid branchId, Guid bankAccountId, string fingerprint, CancellationToken ct);
    Task<BankStatementLine?> FindIdentityAsNoTracking(Guid branchId, Guid bankAccountId, string identity, CancellationToken ct);
    Task AddImport(BankStatementImport import, CancellationToken ct);
    Task AddLine(BankStatementLine line, CancellationToken ct);
    Task<BankStatementImport?> GetImportAsNoTracking(Guid id, Guid branchId, CancellationToken ct);
    Task<IReadOnlyList<BankReceipt>> PendingReceiptsAsNoTracking(Guid branchId, Guid bankAccountId, CancellationToken ct);
    Task AddProposal(BankMatchProposal proposal, CancellationToken ct);
    Task<BankMatchProposal?> GetProposalAsNoTracking(Guid id, Guid branchId, CancellationToken ct);
    Task<bool> LineMatched(Guid lineId, CancellationToken ct);
    Task<bool> ReceiptMatched(Guid receiptId, CancellationToken ct);
    Task<BankReceipt?> GetReceipt(Guid id, Guid branchId, CancellationToken ct);
    Task AddMatch(BankStatementMatch match, CancellationToken ct);
    Task<bool> ReceiptRequiresReview(Guid receiptId, CancellationToken ct);
}
