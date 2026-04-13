using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.OperatorAccounts.GetOperatorSelfContext;

public static class GetOperatorSelfContextMapper
{
    public static ResponseSelfContextJson ToSelfContextResponse(
        Operator? op,
        IReadOnlyList<OperatorAccount> links)
    {
        if (op is null)
        {
            return new ResponseSelfContextJson();
        }

        var accountItems = links.Select(link => link.ToResponseWithNavigation()).ToList();
        var primary = accountItems.FirstOrDefault(a => a.IsPrimary);

        return new ResponseSelfContextJson
        {
            OperatorId = op.Id,
            OperatorName = op.Name,
            PrimaryAccount = primary,
            AvailableAccounts = accountItems
        };
    }
}
