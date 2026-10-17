using server.Application.Services.Members;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Clients.FiadoStatement;

/// <summary>
/// <c>GET /client/{clientId}/fiado-statement</c> (M7.7 Phase 5.5). Fiado is the client's debt with the branch, so
/// the statement is always branch-wide across every Tab; the account scope only decides <b>who</b> may read it:
/// Manager/Admin always, a Member only while holding at least one active explicit Tab grant (decision 1.8 — a
/// Terminal grant never implies fiado authority). Balances come from the shared fiado netting projection.
/// </summary>
public class GetClientFiadoStatementUseCase(
    IAuthenticationService authenticationService,
    IClientsRepository clientsRepository,
    IAccountsRepository accountsRepository,
    ITransactionsRepository transactionsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    IBranchClock branchClock)
{
    public async Task<ResponseClientFiadoStatementJson> Execute(
        Guid clientId,
        RequestClientFiadoStatementJson request,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role == Role.Member)
            await EnsureMemberHoldsTabGrant(branchUser.UserId, branchUser.BranchId, ct);

        Validate(request);

        var client = await clientsRepository.GetByIdAndBranchIdAsNoTracking(clientId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);

        var today = branchClock.LocalBusinessDate(branchClock.UtcNow());
        var dateFrom = request.DateFrom?.Date;
        var dateTo = (request.DateTo ?? today).Date;

        // The window is validated as resolved: an explicit DateFrom after the defaulted DateTo (today) is inverted
        // exactly like two explicit inverted dates.
        if (dateFrom > dateTo)
            throw new OnValidationException([ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED]);

        var position = await transactionsRepository.GetFiadoClientPositionAsNoTracking(
            branchUser.BranchId, client.Id, today, ct);
        var window = await transactionsRepository.GetFiadoStatementWindowByClientIdAsNoTracking(
            branchUser.BranchId, client.Id, dateFrom, dateTo, ct);
        var rows = await transactionsRepository.ListFiadoStatementByClientIdAsNoTracking(
            branchUser.BranchId, client.Id, dateFrom, dateTo, request.Page, request.PageSize, ct);

        var totalPages = window.TotalCount == 0 ? 0 : (int)Math.Ceiling((double)window.TotalCount / request.PageSize);

        return new ResponseClientFiadoStatementJson
        {
            ClientId = client.Id,
            ClientName = client.Name,
            AsOfDate = today,
            OutstandingTotal = position.OutstandingTotal,
            OldestUnpaidDate = position.OldestUnpaidDate,
            DateFrom = dateFrom,
            DateTo = dateTo,
            OpeningBalance = window.OpeningBalance,
            ClosingBalance = window.ClosingBalance,
            Items = rows
                .Select(row => new ResponseClientFiadoStatementItemJson
                {
                    TransactionId = row.Id,
                    Date = row.Date,
                    Direction = row.Direction,
                    Value = row.Value,
                    AccountId = row.AccountId,
                    AccountName = row.AccountName,
                    TransactionTypeName = row.TransactionTypeName,
                    Description = row.Description,
                    RunningBalance = row.RunningBalance
                })
                .ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = window.TotalCount,
            TotalPages = totalPages,
            HasNext = request.Page < totalPages,
            HasPrevious = request.Page > 1
        };
    }

    private async Task EnsureMemberHoldsTabGrant(Guid userId, Guid branchId, CancellationToken ct)
    {
        var scope = await memberAccountScopeResolver.Resolve(userId, branchId, ct);
        if (scope.LinkedOperator is null || scope.AllowedAccountIds.Count == 0)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.CLIENT_FIADO_STATEMENT_ACCESS_DENIED);

        var activeAccounts = await accountsRepository.ListActiveByBranchId(branchId);
        var holdsTab = activeAccounts.Any(account =>
            account.Type == AccountType.Tab && scope.AllowedAccountIds.Contains(account.Id));

        if (holdsTab is false)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.CLIENT_FIADO_STATEMENT_ACCESS_DENIED);
    }

    private static void Validate(RequestClientFiadoStatementJson request)
    {
        var result = new ClientFiadoStatementFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}
