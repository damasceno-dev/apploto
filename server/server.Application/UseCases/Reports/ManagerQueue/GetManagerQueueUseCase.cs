using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.ManagerQueue;

public class GetManagerQueueUseCase(
    IAuthenticationService authenticationService,
    ManagerQueueReader managerQueueReader,
    IBranchClock branchClock)
{
    public async Task<ResponseManagerQueueJson> Execute(
        RequestManagerQueueJson request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        var branchLocalToday = branchClock.LocalBusinessDate(branchClock.UtcNow());

        return await managerQueueReader.Read(branchUser.BranchId, request.Date.Date, branchLocalToday, ct);
    }

    private static void Validate(RequestManagerQueueJson request)
    {
        var result = new ManagerQueueFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
