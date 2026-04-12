using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.OperatorAccounts.AssignAccount;

public class AssignAccountUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    IAccountsRepository accountsRepository,
    IOperatorAccountsRepository operatorAccountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseOperatorAccountJson> Execute(Guid operatorId, RequestAssignAccountJson request)
    {
        Validate(operatorId, request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        _ = await operatorsRepository.GetActiveByIdAndBranchIdAsNoTracking(operatorId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        var account = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.AccountId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        var existingLink = await operatorAccountsRepository.GetByOperatorIdAndAccountId(operatorId, request.AccountId);

        OperatorAccount link;

        if (existingLink is null)
        {
            link = new OperatorAccount { OperatorId = operatorId, AccountId = request.AccountId };
            await operatorAccountsRepository.Add(link);
        }
        else if (existingLink.Active is false)
        {
            existingLink.Active = true;
            link = existingLink;
        }
        else
        {
            throw new ConflictException(ResourcesErrorMessages.OPERATOR_ACCOUNT_ALREADY_ACTIVE);
        }

        await unitOfWork.Commit();

        return link.ToResponse(account);
    }

    private static void Validate(Guid operatorId, RequestAssignAccountJson request)
    {
        List<string> errors = [];

        if (operatorId == Guid.Empty)
            errors.Add(ResourcesErrorMessages.OPERATOR_ID_EMPTY);

        var result = new AssignAccountFluentValidation().Validate(request);
        errors.AddRange(result.Errors.Select(e => e.ErrorMessage));

        if (errors.Count > 0)
            throw new OnValidationException(errors);
    }
}
