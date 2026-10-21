using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.ManagementOverview;

/// <summary>
/// <c>GET /report/management-overview</c> (M11 Phase 7): the Dashboard's read-only summary of every manager report.
/// Validation, authentication and role checks fail before any section reads; the clock is read once; every section
/// runs inside one read-only <c>REPEATABLE READ</c> snapshot, and any failure or cancellation fails the whole request.
/// It never commits (no <c>IUnitOfWork</c>).
/// </summary>
public class GetManagementOverviewUseCase(
    IAuthenticationService authenticationService,
    IBranchClock branchClock,
    IReportReadSnapshot reportReadSnapshot,
    ManagementOverviewBuilder managementOverviewBuilder)
{
    public async Task<ResponseManagementOverviewJson> Execute(
        RequestManagementOverviewJson request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        // The one clock read: GeneratedAtUtc, today and the branch-local "now" of in-progress hours all derive from it.
        var generatedAtUtc = branchClock.UtcNow();
        var branchLocalNow = branchClock.LocalBusinessDateTime(generatedAtUtc);
        var asOfDate = branchLocalNow.Date;
        var year = request.Year ?? asOfDate.Year;
        var month = request.Month ?? asOfDate.Month;

        if (MonthlyFinancialReader.IsFutureMonth(year, month, asOfDate))
            throw new OnValidationException([ResourcesErrorMessages.REPORT_MONTH_IN_FUTURE]);

        await using var snapshot = await reportReadSnapshot.Begin(ct);
        return await managementOverviewBuilder.Build(
            branchUser.BranchId, generatedAtUtc, branchLocalNow, asOfDate, year, month, ct);
    }

    private static void Validate(RequestManagementOverviewJson request)
    {
        var result = new ManagementOverviewFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
