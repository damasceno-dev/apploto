using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Accounts.List;

public static class ListAccountsMapper
{
    extension(IEnumerable<Account> accounts)
    {
        public ResponseListAccountsJson ToResponse(IReadOnlyDictionary<Guid, Guid> terminalIdsByTabAccountId)
        {
            return new ResponseListAccountsJson
            {
                Accounts = accounts
                    .Select(a => a.ToAccountResponse(
                        terminalIdsByTabAccountId.TryGetValue(a.Id, out var terminalAccountId)
                            ? terminalAccountId
                            : null))
                    .ToList()
            };
        }
    }
}
