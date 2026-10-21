using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.MonthlyReconciliation;

public class GetMonthlyReconciliationUseCase(
    IAuthenticationService authenticationService,
    MonthlyReconciliationReader monthlyReconciliationReader,
    IBranchClock branchClock)
{
    public async Task<ResponseMonthlyReconciliationJson> Execute(
        RequestMonthlyReconciliationJson request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        var branchLocalToday = branchClock.LocalBusinessDate(branchClock.UtcNow());

        return await monthlyReconciliationReader.Read(branchUser.BranchId, request.Year, request.Month, branchLocalToday, ct);
    }

    private static void Validate(RequestMonthlyReconciliationJson request)
    {
        var result = new MonthlyReconciliationFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
