using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Branches.Create;

public class CreateBranchUseCase(
    IAuthenticationService authenticationService,
    IBranchesRepository branchesRepository,
    IBranchUsersRepository branchUsersRepository,
    ICategoriesRepository categoriesRepository,
    ITransactionTypesRepository transactionTypesRepository,
    IProductsRepository productsRepository,
    ISettingsRepository settingsRepository,
    ITimeEntryPoliciesRepository timeEntryPoliciesRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateBranchJson> Execute(RequestCreateBranchJson request)
    {
        await Validate(request);

        var authenticatedUser = await authenticationService.GetAuthenticatedUser();
        var branch = request.ToDomain();
        var creatorBranchUser = branch.ToCreatorBranchUser(authenticatedUser.Id);
        var categories = CreateBranchSeedFactory.CreateDefaultCategories(branch.Id);
        var transactionTypes = CreateBranchSeedFactory.CreateDefaultTransactionTypes(categories);
        var products = CreateBranchSeedFactory.CreateDefaultProducts(branch.Id);
        var setting = CreateBranchSeedFactory.CreateDefaultSetting(branch.Id);
        var timeEntryPolicy = CreateBranchSeedFactory.CreateDefaultTimeEntryPolicy(branch.Id);

        await branchesRepository.Add(branch);
        await branchUsersRepository.Add(creatorBranchUser);
        await categoriesRepository.AddRange(categories);
        await transactionTypesRepository.AddRange(transactionTypes);
        await productsRepository.AddRange(products);
        await settingsRepository.Add(setting);
        await timeEntryPoliciesRepository.Add(timeEntryPolicy);

        await unitOfWork.Commit();

        return branch.ToResponse();
    }

    private static async Task Validate(RequestCreateBranchJson request)
    {
        var result = await new CreateBranchFluentValidation().ValidateAsync(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
