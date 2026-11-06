using server.Application.Services.DailyCloses;
using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.PersonalDifferences;

public class GetPersonalDifferencesUseCase(
    IAuthenticationService authenticationService,
    PersonalDifferencesReader reader,
    ICashVarianceProductResolver varianceProduct,
    IReportReadSnapshot snapshot,
    IBranchClock clock)
{
    public async Task<ResponsePersonalDifferencesJson> Execute(RequestPersonalDifferencesJson request, CancellationToken ct = default)
    {
        var actor = await authenticationService.GetAuthenticatedBranchUser();
        if (actor.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        var validation = new PersonalDifferencesFluentValidation().Validate(request);
        if (!validation.IsValid) throw new OnValidationException(validation.Errors.Select(e => e.ErrorMessage).ToList());
        var today = clock.LocalBusinessDate(clock.UtcNow());
        var cutoff = request.AsOfDate ?? today;
        if (cutoff > today || MonthlyFinancialReader.IsFutureMonth(request.Year, request.Month, cutoff))
            throw new OnValidationException([ResourcesErrorMessages.REPORT_MONTH_IN_FUTURE]);
        await using var read = await snapshot.Begin(ct);
        var product = await varianceProduct.GetIdAsync(actor.BranchId, ct);
        return await reader.Read(actor.BranchId, product, request.Year, request.Month, cutoff,
            request.OperatorId, request.Page, request.PageSize,
            applyCurrentThreshold: request.Year == today.Year && request.Month == today.Month && cutoff == today, ct: ct);
    }
}
