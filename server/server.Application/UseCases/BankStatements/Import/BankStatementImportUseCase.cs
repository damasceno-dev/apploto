using server.Application.Services.Banking;
using server.Application.Services.Receivables;
using server.Application.UseCases.Cheques;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.BankStatements.Import;

public class BankStatementImportUseCase(
    BankCommandRunner runner,
    IBankStatementsRepository statements,
    IAccountsRepository accounts,
    IUnitOfWork unitOfWork,
    BankStatementResponseBuilder responseBuilder)
{
    public Task<ResponseBankStatementJson> Execute(RequestBankStatementImportJson request, string? key, CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new BankStatementImportFluentValidation(), request);
        byte[] original;
        OfxStatementEvidence evidence;
        try
        {
            original = Convert.FromBase64String(request.ContentBase64);
            evidence = OfxStatementParser.Parse(original, ct);
        }
        catch (FormatException) { throw new OnValidationException([ResourcesErrorMessages.OFX_INVALID]); }
        if (evidence.AccountId != request.AccountIdentity || evidence.BankId != request.BankIdentity)
            throw new OnValidationException([ResourcesErrorMessages.OFX_INVALID]);
        return runner.Run<RequestBankStatementImportJson, ResponseBankStatementJson>("POST /bank-statement/import", request, key,
            (_, _) => Task.FromResult<IReadOnlyCollection<Guid>>([request.BankAccountId]),
            async (actor, token) =>
            {
                var bank = await accounts.GetActiveByIdAndBranchIdAsNoTracking(request.BankAccountId, actor.BranchId, token);
                if (bank is null || bank.Type != AccountType.BankAccount) throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_BANK_NOT_FOUND);
                var binding = await statements.GetAccountBindingAsNoTracking(request.AccountBindingId, actor.BranchId, token);
                if (binding is null || binding.BankAccountId != bank.Id || binding.BankIdentity != evidence.BankId || binding.AccountIdentity != evidence.AccountId)
                    throw new OnValidationException([ResourcesErrorMessages.OFX_INVALID]);
                var existing = await statements.FindImportAsNoTracking(actor.BranchId, bank.Id, evidence.Fingerprint, token);
                if (existing is not null) return (existing.Id, await responseBuilder.Build(existing, true, token));
                var import = new BankStatementImport
                {
                    BranchId = actor.BranchId, BankAccountId = bank.Id, AccountBindingId = binding.Id, Fingerprint = evidence.Fingerprint,
                    Original = original, From = evidence.From, To = evidence.To, CreatedByUserId = actor.UserId
                };
                foreach (var item in evidence.Lines)
                {
                    var line = item.BankIdentity is null ? null : await statements.FindIdentityAsNoTracking(actor.BranchId, bank.Id, item.BankIdentity, token);
                    if (line is not null && line.Fingerprint != item.Fingerprint &&
                        OfxStatementParser.CanonicalLineFingerprint(line.Original, line.PostedOn, line.Amount) != item.Fingerprint) throw new ConflictException(ResourcesErrorMessages.BANKING_STATE_CONFLICT);
                    if (line is null)
                    {
                        line = new BankStatementLine
                        {
                            BranchId = actor.BranchId, BankAccountId = bank.Id, BankIdentity = item.BankIdentity,
                            Fingerprint = item.Fingerprint, PostedOn = item.PostedOn, Amount = item.Amount, Type = item.Type,
                            Name = item.Name, Memo = item.Memo, Original = item.Original, AmbiguousIdentity = item.BankIdentity is null
                        };
                        await statements.AddLine(line, token);
                    }
                    import.Lines.Add(new BankStatementImportLine { ImportId = import.Id, LineId = line.Id });
                }
                await statements.AddImport(import, token);
                await unitOfWork.Commit(token);
                return (import.Id, await responseBuilder.Build((await statements.GetImportAsNoTracking(import.Id, actor.BranchId, token))!, false, token));
            }, ct);
    }

}
