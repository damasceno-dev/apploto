using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using server.Application.Services.Receivables;
using server.Application.UseCases.Cheques;
using server.Application.UseCases.BankStatements.Propose;
using server.Application.UseCases.BankStatements.Confirm;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Banking;

public class BankMatchService(
    BankCommandRunner runner,
    IBankStatementsRepository statements,
    IAccountsRepository accounts,
    ISettlementEventsRepository events,
    ITransactionsRepository transactions,
    ReceivableResponseBuilder responses,
    SettlementEventDating dating)
{
    public Task<ResponseBankMatchProposalsJson> Propose(RequestBankMatchProposeJson request, string? key, CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new BankMatchProposeFluentValidation(), request);
        return runner.Run<RequestBankMatchProposeJson, ResponseBankMatchProposalsJson>("POST /bank-statement/propose", request, key,
            async (actor, token) => [(await Import(request.ImportId, actor.BranchId, token)).BankAccountId],
            async (actor, token) =>
            {
                var import = await Import(request.ImportId, actor.BranchId, token);
                if (import.AccountBindingId is null) throw Conflict();
                await EnsureBank(import.BankAccountId, actor.BranchId, token);
                var receipts = await statements.PendingReceiptsAsNoTracking(actor.BranchId, import.BankAccountId, token);
                if (receipts.Count > 1000) throw Conflict();
                var expectations = new List<(BankReceipt Row, ResponseBankReceiptJson Read)>();
                foreach (var receipt in receipts)
                {
                    if (!await statements.ReceiptRequiresReview(receipt.Id, token))
                        expectations.Add((receipt, (await responses.BuildReceipt(receipt.Id, actor.BranchId, token))!));
                }
                var proposals = new List<ResponseBankMatchProposalJson>();
                var unmatched = new List<Guid>();
                foreach (var link in import.Lines.OrderBy(l => l.LineId))
                {
                    var line = link.Line;
                    if (await statements.LineMatched(line.Id, token)) continue;
                    var candidates = line.AmbiguousIdentity ? [] : expectations.Where(e =>
                        e.Read.ExpectedAmount > 0 && Signed(e.Row.SourceKind, e.Read.ExpectedAmount) == line.Amount &&
                        line.PostedOn >= e.Row.OccurredOn).ToList();
                    if (candidates.Count == 0) unmatched.Add(line.Id);
                    foreach (var candidate in candidates)
                    {
                        if (proposals.Count >= 5000) throw Conflict();
                        var proposal = new BankMatchProposal
                        {
                            BranchId = actor.BranchId, BankAccountId = import.BankAccountId,
                            LineId = line.Id, LineVersion = line.Version, ReceiptId = candidate.Row.Id,
                            ReceiptVersion = candidate.Row.Version, Amount = line.Amount,
                            ExpectationFingerprint = Fingerprint(candidate.Read), CreatedByUserId = actor.UserId
                        };
                        await statements.AddProposal(proposal, token);
                        proposals.Add(Map(proposal));
                    }
                }
                return (import.Id, new ResponseBankMatchProposalsJson { ImportId = import.Id, Proposals = proposals, UnmatchedLineIds = unmatched });
            }, ct);
    }

    public Task<ResponseBankMatchConfirmationJson> Confirm(RequestBankMatchConfirmJson request, string? key, CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new BankMatchConfirmFluentValidation(), request);
        return runner.Run<RequestBankMatchConfirmJson, ResponseBankMatchConfirmationJson>("POST /bank-statement/confirm", request, key,
            async (actor, token) =>
            {
                var ids = new List<Guid>();
                foreach (var item in request.Items)
                {
                    var proposal = await Proposal(item.ProposalId, actor.BranchId, token);
                    ids.Add(proposal.BankAccountId);
                    // Same coordination as returns/cancellation of each source claim, before any confirmation read.
                    foreach (var allocation in await events.ListByBankReceiptIdAndBranchIdAsNoTracking(proposal.ReceiptId, actor.BranchId, token))
                    {
                        var row = await transactions.GetByIdAndBranchIdAsNoTracking(allocation.ReceivableTransactionId, actor.BranchId, token) ?? throw Conflict();
                        ids.Add(row.AccountId);
                    }
                }
                return ids;
            },
            async (actor, token) =>
            {
                var ids = new List<Guid>();
                foreach (var item in request.Items)
                {
                    var proposal = await Proposal(item.ProposalId, actor.BranchId, token);
                    await EnsureBank(proposal.BankAccountId, actor.BranchId, token);
                    var receipt = await statements.GetReceipt(proposal.ReceiptId, actor.BranchId, token) ?? throw Conflict();
                    var read = await responses.BuildReceipt(receipt.Id, actor.BranchId, token) ?? throw Conflict();
                    if (proposal.LineId != item.LineId || proposal.ReceiptId != item.ReceiptId || proposal.Amount != item.Amount ||
                        proposal.LineVersion != item.LineVersion || proposal.ReceiptVersion != item.ReceiptVersion ||
                        proposal.ExpectationFingerprint != item.ExpectationFingerprint ||
                        proposal.Line.Version != item.LineVersion || receipt.Version != item.ReceiptVersion ||
                        receipt.Status != BankReceiptStatus.Pending || proposal.Line.AmbiguousIdentity ||
                        proposal.Line.Amount != item.Amount || Signed(receipt.SourceKind, read.ExpectedAmount) != item.Amount ||
                        Fingerprint(read) != item.ExpectationFingerprint || await statements.ReceiptRequiresReview(receipt.Id, token) ||
                        await statements.LineMatched(item.LineId, token) || await statements.ReceiptMatched(item.ReceiptId, token)) throw Conflict();
                    var dates = await dating.Resolve(actor.BranchId, proposal.Line.PostedOn, request.PostedOn, token);
                    if (dates.PostedOn < receipt.PostedOn || read.FeeEvidenceHistory.Any(e => e.PostedOn > dates.PostedOn)) throw Conflict();
                    var match = new BankStatementMatch
                    {
                        BranchId = actor.BranchId, ProposalId = proposal.Id, LineId = item.LineId, ReceiptId = item.ReceiptId,
                        Amount = item.Amount, PostedOn = dates.PostedOn, CreatedByUserId = actor.UserId
                    };
                    await statements.AddMatch(match, token);
                    receipt.Status = BankReceiptStatus.Confirmed;
                    ids.Add(match.Id);
                }
                return (ids[0], new ResponseBankMatchConfirmationJson { MatchIds = ids });
            }, ct);
    }

    private static decimal Signed(BankReceiptSourceKind kind, decimal amount) => kind is BankReceiptSourceKind.BankFee or BankReceiptSourceKind.ChequeReturn ? -amount : amount;

    internal static string Fingerprint(ResponseBankReceiptJson read)
    {
        var value = read.Id.ToString("N") + ":" + read.FeeEvidenceVersion + ":" + read.Version + ":" + read.ExpectedAmount.ToString(CultureInfo.InvariantCulture) + ":" +
            string.Join(";", read.Allocations.OrderBy(a => a.SettlementEventId).Select(a => a.SettlementEventId.ToString("N") + ":" + a.ReturnedByEventId));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
    private async Task EnsureBank(Guid id, Guid branch, CancellationToken ct)
    {
        var bank = await accounts.GetActiveByIdAndBranchIdAsNoTracking(id, branch, ct);
        if (bank is null || bank.Type != AccountType.BankAccount) throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_BANK_NOT_FOUND);
    }
    private async Task<BankStatementImport> Import(Guid id, Guid branch, CancellationToken ct) => await statements.GetImportAsNoTracking(id, branch, ct) ?? throw Conflict();
    private async Task<BankMatchProposal> Proposal(Guid id, Guid branch, CancellationToken ct) => await statements.GetProposalAsNoTracking(id, branch, ct) ?? throw Conflict();
    private static ConflictException Conflict() => new(ResourcesErrorMessages.BANKING_STATE_CONFLICT);
    private static ResponseBankMatchProposalJson Map(BankMatchProposal p) => new()
    {
        Id = p.Id, LineId = p.LineId, LineVersion = p.LineVersion, ReceiptId = p.ReceiptId, ReceiptVersion = p.ReceiptVersion,
        Amount = p.Amount, ExpectationFingerprint = p.ExpectationFingerprint
    };
}
