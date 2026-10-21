using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.UpcomingSettlements;

public class GetUpcomingSettlementsUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    UpcomingSettlementsReader upcomingSettlementsReader,
    IBranchClock branchClock)
{
    public async Task<ResponseUpcomingSettlementsJson> Execute(
        RequestUpcomingSettlementsJson request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        // A deactivated account may still hold unpaid classified rows, so any account of the branch is accepted.
        if (request.AccountId is { } accountId &&
            await accountsRepository.GetByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId, ct) is null)
            throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        var asOfDate = branchClock.LocalBusinessDate(branchClock.UtcNow());

        return await upcomingSettlementsReader.Read(
            branchUser.BranchId,
            asOfDate,
            request.DueDateFrom,
            request.DueDateTo,
            request.AccountId,
            request.Instrument,
            request.Page,
            request.PageSize,
            ct);
    }

    private static void Validate(RequestUpcomingSettlementsJson request)
    {
        var result = new UpcomingSettlementsFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
