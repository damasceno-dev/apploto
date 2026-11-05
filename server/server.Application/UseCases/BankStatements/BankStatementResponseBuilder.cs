using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Application.UseCases.BankStatements;

public class BankStatementResponseBuilder(IBankStatementsRepository statements)
{
    public async Task<ResponseBankStatementJson> Build(BankStatementImport import, bool duplicate, CancellationToken ct)
    {
        var lines = new List<ResponseBankStatementLineJson>();
        foreach (var link in import.Lines.OrderBy(l => l.Line.PostedOn).ThenBy(l => l.Line.Id))
        {
            var l = link.Line;
            lines.Add(new ResponseBankStatementLineJson
            {
                Id = l.Id, Version = l.Version, BankIdentity = l.BankIdentity, PostedOn = l.PostedOn,
                Amount = l.Amount, Name = l.Name, Memo = l.Memo, AmbiguousIdentity = l.AmbiguousIdentity,
                Matched = await statements.LineMatched(l.Id, ct)
            });
        }
        return new ResponseBankStatementJson { Id = import.Id, BankAccountId = import.BankAccountId, AccountBindingId = import.AccountBindingId, DuplicateImport = duplicate, Lines = lines };
    }
}
