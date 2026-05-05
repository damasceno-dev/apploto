using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Holidays.List;

public class ListHolidaysUseCase(
    IAuthenticationService authenticationService,
    IHolidaysRepository holidaysRepository)
{
    public async Task<ResponseListHolidaysJson> Execute(RequestListHolidaysJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var filter = request.ToFilter();

        var items = await holidaysRepository.ListByBranchIdAsNoTracking(branchUser.BranchId, filter);
        var totalCount = await holidaysRepository.CountByBranchIdAsNoTracking(branchUser.BranchId, filter);

        return items.ToListResponse(filter, totalCount);
    }

    private static void Validate(RequestListHolidaysJson request)
    {
        var result = new ListHolidaysFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
