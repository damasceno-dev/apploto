using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Accounts.Create;

public static class CreateAccountMapper
{
    public static Account ToDomain(this RequestCreateAccountJson request, Guid branchId)
    {
        return new Account
        {
            Type = request.Type,
            Name = request.Name.Trim(),
            Institution = request.Institution?.Trim(),
            Number = request.Number?.Trim(),
            BranchId = branchId,
            TabAccountId = request.TabAccountId
        };
    }

    extension(Account account)
    {
        public ResponseCreateAccountJson ToCreateResponse()
        {
            return new ResponseCreateAccountJson
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
