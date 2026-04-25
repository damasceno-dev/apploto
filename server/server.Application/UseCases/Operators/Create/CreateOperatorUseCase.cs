using server.Communication.Requests;
using server.Communication.Responses;
using server.Application.Services.Operators;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Operators.Create;

public class CreateOperatorUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    IOperatorUserLinkGuard operatorUserLinkGuard,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateOperatorJson> Execute(RequestCreateOperatorJson request)
    {
        Validate(request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (request.UserId.HasValue)
        {
            await operatorUserLinkGuard.EnsureLinkable(request.UserId.Value, authenticatedBranchUser.BranchId);
        }

        var op = request.ToDomain(authenticatedBranchUser.BranchId);

        await operatorsRepository.Add(op);
        await unitOfWork.Commit();

        return op.ToResponse();
    }

    private static void Validate(RequestCreateOperatorJson request)
    {
        var result = new CreateOperatorFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
