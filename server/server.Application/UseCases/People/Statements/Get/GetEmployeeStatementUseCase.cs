using server.Application.Services.Transactions;
using server.Application.Services.Reports;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
namespace server.Application.UseCases.People;

public class GetEmployeeStatementUseCase(IAuthenticationService authentication, EmployeeStatementReader reader, IReportReadSnapshot snapshot, IBranchClock clock)
{
    public async Task<ResponseEmployeeStatementJson> Execute(RequestEmployeeStatementJson request, CancellationToken ct = default, bool printable = false)
    {
        var actor = await authentication.GetAuthenticatedBranchUser();
        if (actor.Role is not Role.Manager and not Role.Admin) throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        PeopleValidation.Validate(new EmployeeStatementFluentValidation(), request);
        var localNow = clock.LocalBusinessDateTime(clock.UtcNow());
        var today = localNow.Date;
        var cutoff = request.AsOfDate ?? today;
        if (cutoff > today || MonthlyFinancialReader.IsFutureMonth(request.Year, request.Month, cutoff)) throw new OnValidationException([ResourcesErrorMessages.REPORT_MONTH_IN_FUTURE]);
        await using var read = await snapshot.Begin(ct);
        return await reader.Read(actor.BranchId, request.OperatorId, request.Year, request.Month, cutoff, printable ? 1 : request.Page, printable ? 10000 : request.PageSize, ct, localNow);
    }
}
