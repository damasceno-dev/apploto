using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.OperatorAccounts.ListOperatorAccounts;

public static class ListOperatorAccountsMapper
{
    extension(IReadOnlyList<OperatorAccount> links)
    {
        public ResponseListOperatorAccountsJson ToListResponse()
        {
            return new ResponseListOperatorAccountsJson
            {
                OperatorAccounts = links
                    .Select(link => link.ToResponseWithNavigation())
                    .ToList()
            };
        }
    }
}
