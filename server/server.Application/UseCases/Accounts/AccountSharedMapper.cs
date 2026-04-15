using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Accounts;

public static class AccountSharedMapper
{
    public static Account ToDomain(
        AccountType type,
        Guid branchId,
        string name,
        string? institution,
        string? number)
    {
        return new Account
        {
            Type = type,
            Name = name.Trim(),
            Institution = institution?.Trim(),
            Number = number?.Trim(),
            BranchId = branchId
        };
    }

    extension(Account account)
    {
        public ResponseAccountJson ToAccountResponse(Guid? terminalAccountId = null)
        {
            return new ResponseAccountJson
            {
                Id = account.Id,
                Type = account.Type,
                Name = account.Name,
                Institution = account.Institution,
                Number = account.Number,
                BranchId = account.BranchId,
                TabAccountId = account.TabAccountId,
                TerminalAccountId = terminalAccountId
            };
        }
        public ResponseCreateAccountJson ToCreateResponse(Guid? terminalAccountId = null)
        {
            return new ResponseCreateAccountJson
            {
                Id = account.Id,
                Type = account.Type,
                Name = account.Name,
                Institution = account.Institution,
                Number = account.Number,
                BranchId = account.BranchId,
                TabAccountId = account.TabAccountId,
                TerminalAccountId = terminalAccountId
            };
        }
    }
}
