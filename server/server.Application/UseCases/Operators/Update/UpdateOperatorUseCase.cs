using server.Communication.Requests;
using server.Communication.Responses;
using server.Application.Services.Operators;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Operators.Update;

public class UpdateOperatorUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    IOperatorUserLinkGuard operatorUserLinkGuard,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Updates mutable Operator fields. Passing <c>UserId = null</c> intentionally clears
    /// the login link while preserving the Operator row for history and reports.
    /// </summary>
    public async Task<ResponseOperatorJson> Execute(Guid operatorId, RequestUpdateOperatorJson request)
    {
        Validate(operatorId, request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var op = await operatorsRepository.GetActiveByIdAndBranchId(operatorId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        if (request.UserId.HasValue)
        {
            await operatorUserLinkGuard.EnsureLinkable(request.UserId.Value, authenticatedBranchUser.BranchId, op.Id);
        }

        op.Name = request.Name.Trim();
        op.UserId = request.UserId;

        await unitOfWork.Commit();

        return op.ToOperatorResponse();
    }

    private static void Validate(Guid operatorId, RequestUpdateOperatorJson request)
    {
        if (operatorId == Guid.Empty)
        {
            throw new OnValidationException([ResourcesErrorMessages.OPERATOR_ID_EMPTY]);
        }

        var result = new UpdateOperatorFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
