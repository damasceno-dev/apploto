using server.Domain.Entities.Enums;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.OperatorAccounts;

public static class OperatorAccountSharedMapper
{
    extension(OperatorAccount link)
    {
        public ResponseOperatorAccountJson ToResponse(Account account)
        {
            return new ResponseOperatorAccountJson
            {
                Id = link.Id,
                OperatorId = link.OperatorId,
                AccountId = link.AccountId,
                IsPrimary = link.IsPrimary,
                AccountType = account.Type,
                AccountName = account.Name,
                AccountInstitution = account.Institution,
                AccountNumber = account.Number,
                PairedTabAccountId = account.Type == AccountType.Terminal ? account.TabAccountId : null
            };
        }

        public ResponseOperatorAccountJson ToResponseWithNavigation()
        {
            return link.ToResponse(link.Account);
        }
    }
}
