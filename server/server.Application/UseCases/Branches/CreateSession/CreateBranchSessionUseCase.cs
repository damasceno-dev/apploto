using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Branches.CreateSession;

public class CreateBranchSessionUseCase(
    IAuthenticationService authenticationService,
    IBranchUsersRepository branchUsersRepository,
    IBranchesRepository branchesRepository,
    ITokenServices tokenServices)
{
    public async Task<ResponseCreateBranchSessionJson> Execute(RequestCreateBranchSessionJson request)
    {
        Validate(request);

        var authenticatedUser = await authenticationService.GetAuthenticatedUser();
        var branchUser = await branchUsersRepository.GetActiveByUserIdAndBranchId(authenticatedUser.Id, request.BranchId);
        if (branchUser is null)
        {
            throw new NotFoundException(ResourcesErrorMessages.BRANCH_NOT_FOUND);
        }

        var branch = await branchesRepository.GetById(branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.BRANCH_NOT_FOUND);

        var token = tokenServices.GenerateBranchToken(branchUser);

        return branch.ToSessionResponse(branchUser.Role, token);
    }

    private static void Validate(RequestCreateBranchSessionJson request)
    {
        var result = new CreateBranchSessionFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
