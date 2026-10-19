using server.Application.UseCases.Branches.Create;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Application.Services.Security;

/// <summary>Internal seed writer for operated onboarding and disposable test fixtures.</summary>
public sealed class BranchBootstrap(
    IBranchesRepository branches, ICategoriesRepository categories,
    ITransactionTypesRepository types, IProductsRepository products,
    ISettingsRepository settings, ITimeEntryPoliciesRepository policies)
{
    public async Task<Branch> Create(RequestCreateBranchJson request)
    {
        var branch = new Branch
        {
            Name = request.Name, Cnpj = request.Cnpj, Address = request.Address, Phone = request.Phone
        };
        var seedCategories = CreateBranchSeedFactory.CreateDefaultCategories(branch.Id);
        await branches.Add(branch);
        await categories.AddRange(seedCategories);
        await types.AddRange(CreateBranchSeedFactory.CreateDefaultTransactionTypes(seedCategories));
        await products.AddRange(CreateBranchSeedFactory.CreateDefaultProducts(branch.Id));
        await settings.Add(CreateBranchSeedFactory.CreateDefaultSetting(branch.Id));
        await policies.Add(CreateBranchSeedFactory.CreateDefaultTimeEntryPolicy(branch.Id));
        return branch;
    }
}
