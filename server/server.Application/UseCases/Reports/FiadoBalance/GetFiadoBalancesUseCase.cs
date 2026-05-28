using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.FiadoBalance;

public class GetFiadoBalancesUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IBranchClock branchClock)
{
    public async Task<ResponseFiadoBalanceJson> Execute(RequestFiadoBalanceJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        var asOfDate = request.AsOfDate ?? branchClock.LocalBusinessDate(branchClock.UtcNow());

        var items = await transactionsRepository
            .ListFiadoBalancesByBranchIdAsNoTracking(branchUser.BranchId, request.ClientId, asOfDate);

        var totalOutstanding = items.Sum(i => i.OutstandingTotal);

        return new ResponseFiadoBalanceJson
        {
            AsOfDate = asOfDate,
            Items = items
                .Select(row => new ResponseFiadoClientBalanceItemJson
                {
                    ClientId = row.ClientId,
                    ClientName = row.ClientName,
                    OutstandingTotal = row.OutstandingTotal
                })
                .ToList(),
            TotalOutstanding = totalOutstanding
        };
    }

    private static void Validate(RequestFiadoBalanceJson request)
    {
        var result = new FiadoBalanceFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
