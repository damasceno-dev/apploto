using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.MonthlyFinancial;

public class GetMonthlyFinancialReportUseCase(
    IAuthenticationService authenticationService,
    MonthlyFinancialReader monthlyFinancialReader,
    IBranchClock branchClock)
{
    public async Task<ResponseMonthlyFinancialReportJson> Execute(
        RequestMonthlyFinancialReportJson request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        // One clock read: today bounds the month window and "now" measures in-progress hours.
        var utcNow = branchClock.UtcNow();
        var branchLocalNow = branchClock.LocalBusinessDateTime(utcNow);
        var asOfDate = branchClock.LocalBusinessDate(utcNow);

        if (MonthlyFinancialReader.IsFutureMonth(request.Year, request.Month, asOfDate))
            throw new OnValidationException([ResourcesErrorMessages.REPORT_MONTH_IN_FUTURE]);

        return await monthlyFinancialReader.Read(branchUser.BranchId, request.Year, request.Month, asOfDate, branchLocalNow, ct);
    }

    private static void Validate(RequestMonthlyFinancialReportJson request)
    {
        var result = new MonthlyFinancialFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
