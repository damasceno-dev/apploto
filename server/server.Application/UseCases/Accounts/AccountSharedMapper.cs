using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Accounts;

public static class AccountSharedMapper
{
    extension(Account account)
    {
        public ResponseAccountJson ToAccountResponse()
        {
            return new ResponseAccountJson
            {
                Id = account.Id,
                Type = account.Type,
                Name = account.Name,
                Institution = account.Institution,
                Number = account.Number,
                BranchId = account.BranchId,
                TabAccountId = account.TabAccountId
            };
        }
    }
}
