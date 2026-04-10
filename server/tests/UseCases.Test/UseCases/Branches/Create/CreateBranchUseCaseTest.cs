using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using server.Application.UseCases.Branches.Create;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Branches.Create;

public class CreateBranchUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateBranch_AndReturnResponseWithRequestFields()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchJsonBuilder().Build();

        var branchesRepository = new BranchesRepositoryBuilder().Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var transactionTypesRepository = new TransactionTypesRepositoryBuilder().Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();

        var useCase = CreateUseCase(
            authenticationService,
            branchesRepository,
            branchUsersRepository,
            categoriesRepository,
            transactionTypesRepository,
            productsRepository,
            settingsRepository,
            unitOfWork);

        var response = await useCase.Execute(request);

        response.Id.ShouldNotBe(Guid.Empty);
        response.Name.ShouldBe(request.Name);
        response.Cnpj.ShouldBe(request.Cnpj);
        response.Address.ShouldBe(request.Address);
        response.Phone.ShouldBe(request.Phone);
        await branchesRepository.Received(1).Add(Arg.Is<Branch>(branch =>
            branch.Id == response.Id &&
            branch.Name == request.Name &&
            branch.Cnpj == request.Cnpj &&
            branch.Address == request.Address &&
            branch.Phone == request.Phone));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldPersistCreatorMembershipAsAdmin()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchJsonBuilder().Build();

        var branchesRepository = new BranchesRepositoryBuilder().Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();

        Branch? persistedBranch = null;
        await branchesRepository.Add(Arg.Do<Branch>(branch => persistedBranch = branch));

        var useCase = CreateUseCase(
            authenticationService,
            branchesRepository,
            branchUsersRepository,
            new CategoriesRepositoryBuilder().Build(),
            new TransactionTypesRepositoryBuilder().Build(),
            new ProductsRepositoryBuilder().Build(),
            new SettingsRepositoryBuilder().Build(),
            unitOfWork);

        await useCase.Execute(request);

        persistedBranch.ShouldNotBeNull();
        await branchUsersRepository.Received(1).Add(Arg.Is<BranchUser>(branchUser =>
            branchUser.UserId == authenticatedUser.Id &&
            branchUser.BranchId == persistedBranch!.Id &&
            branchUser.Role == Role.Admin &&
            branchUser.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldSeedExactlyNineDefaultCategories()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchJsonBuilder().Build();

        var branchesRepository = new BranchesRepositoryBuilder().Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();

        Branch? persistedBranch = null;
        await branchesRepository.Add(Arg.Do<Branch>(branch => persistedBranch = branch));

        IReadOnlyList<Category>? persistedCategories = null;
        await categoriesRepository.AddRange(Arg.Do<IEnumerable<Category>>(categories =>
            persistedCategories = categories.ToList()));

        var useCase = CreateUseCase(
            authenticationService,
            branchesRepository,
            new BranchUsersRepositoryBuilder().Build(),
            categoriesRepository,
            new TransactionTypesRepositoryBuilder().Build(),
            new ProductsRepositoryBuilder().Build(),
            new SettingsRepositoryBuilder().Build(),
            unitOfWork);

        await useCase.Execute(request);

        persistedCategories.ShouldNotBeNull();
        persistedCategories!.Count.ShouldBe(9);
        persistedCategories.ShouldAllBe(category => category.BranchId == persistedBranch!.Id);

        var expectedCategories = new (string Name, Direction DefaultDirection)[]
        {
            ("Receita", Direction.In),
            ("Crédito Banco", Direction.In),
            ("Entradas", Direction.In),
            ("Despesas Administrativas", Direction.Out),
            ("Despesas Comerciais", Direction.Out),
            ("Despesas Pessoal", Direction.Out),
            ("Despesas Financeiras", Direction.Out),
            ("Débito Banco", Direction.Out),
            ("Saídas", Direction.Out)
        };

        foreach (var (name, direction) in expectedCategories)
        {
            persistedCategories.ShouldContain(category =>
                category.Name == name && category.DefaultDirection == direction);
        }

        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldSeedExactlyNineteenDefaultTransactionTypes_LinkedToSeededCategories()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchJsonBuilder().Build();

        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var transactionTypesRepository = new TransactionTypesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();

        IReadOnlyList<Category>? persistedCategories = null;
        await categoriesRepository.AddRange(Arg.Do<IEnumerable<Category>>(categories =>
            persistedCategories = categories.ToList()));

        IReadOnlyList<TransactionType>? persistedTransactionTypes = null;
        await transactionTypesRepository.AddRange(Arg.Do<IEnumerable<TransactionType>>(types =>
            persistedTransactionTypes = types.ToList()));

        var useCase = CreateUseCase(
            authenticationService,
            new BranchesRepositoryBuilder().Build(),
            new BranchUsersRepositoryBuilder().Build(),
            categoriesRepository,
            transactionTypesRepository,
            new ProductsRepositoryBuilder().Build(),
            new SettingsRepositoryBuilder().Build(),
            unitOfWork);

        await useCase.Execute(request);

        persistedCategories.ShouldNotBeNull();
        persistedTransactionTypes.ShouldNotBeNull();
        persistedTransactionTypes!.Count.ShouldBe(19);

        var categoryIdsByName = persistedCategories!.ToDictionary(category => category.Name, category => category.Id);

        var expectedTransactionTypes = new (string Name, string CategoryName)[]
        {
            ("Cliente", "Saídas"),
            ("Depósito Dinheiro", "Saídas"),
            ("Cartão de Crédito", "Saídas"),
            ("MarketPlace", "Saídas"),
            ("Sobra de Bolão", "Despesas Comerciais"),
            ("Sobra de Federal", "Despesas Comerciais"),
            ("Depósito Cheque", "Saídas"),
            ("PIX", "Saídas"),
            ("Cartão de Débito", "Saídas"),
            ("Telesena", "Saídas"),
            ("Troca de Telesena", "Saídas"),
            ("Raspadinha", "Saídas"),
            ("Encalhe Federal", "Saídas"),
            ("Cliente", "Entradas"),
            ("Pgto Prêmio", "Saídas"),
            ("Desconto", "Despesas Comerciais"),
            ("Volante rejeitado", "Despesas Comerciais"),
            ("Tarifa cartão", "Despesas Comerciais"),
            ("Outras Despesas", "Despesas Comerciais")
        };

        foreach (var (name, categoryName) in expectedTransactionTypes)
        {
            persistedTransactionTypes.ShouldContain(type =>
                type.Name == name && type.CategoryId == categoryIdsByName[categoryName]);
        }

        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldSeedExactlyEightDefaultProducts()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchJsonBuilder().Build();

        var branchesRepository = new BranchesRepositoryBuilder().Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();

        Branch? persistedBranch = null;
        await branchesRepository.Add(Arg.Do<Branch>(branch => persistedBranch = branch));

        IReadOnlyList<Product>? persistedProducts = null;
        await productsRepository.AddRange(Arg.Do<IEnumerable<Product>>(products =>
            persistedProducts = products.ToList()));

        var useCase = CreateUseCase(
            authenticationService,
            branchesRepository,
            new BranchUsersRepositoryBuilder().Build(),
            new CategoriesRepositoryBuilder().Build(),
            new TransactionTypesRepositoryBuilder().Build(),
            productsRepository,
            new SettingsRepositoryBuilder().Build(),
            unitOfWork);

        await useCase.Execute(request);

        persistedProducts.ShouldNotBeNull();
        persistedProducts!.Count.ShouldBe(8);
        persistedProducts.ShouldAllBe(product => product.BranchId == persistedBranch!.Id);

        var expectedProducts = new (string Name, int DisplayOrder)[]
        {
            ("Telesena", 1),
            ("Raspadinha", 2),
            ("Jogos", 3),
            ("Loteria Especial", 4),
            ("Dinheiro", 5),
            ("Tarifa Bolão", 6),
            ("Federal", 7),
            ("Diferença Caixa", 8)
        };

        foreach (var (name, displayOrder) in expectedProducts)
        {
            persistedProducts.ShouldContain(product =>
                product.Name == name && product.DisplayOrder == displayOrder);
        }

        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldSeedOneSettingRowWithSpecDefaults()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchJsonBuilder().Build();

        var branchesRepository = new BranchesRepositoryBuilder().Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();

        Branch? persistedBranch = null;
        await branchesRepository.Add(Arg.Do<Branch>(branch => persistedBranch = branch));

        var useCase = CreateUseCase(
            authenticationService,
            branchesRepository,
            new BranchUsersRepositoryBuilder().Build(),
            new CategoriesRepositoryBuilder().Build(),
            new TransactionTypesRepositoryBuilder().Build(),
            new ProductsRepositoryBuilder().Build(),
            settingsRepository,
            unitOfWork);

        await useCase.Execute(request);

        await settingsRepository.Received(1).Add(Arg.Is<Setting>(setting =>
            setting.BranchId == persistedBranch!.Id &&
            setting.DailyTargetHours == 7.33m &&
            setting.LunchDeductionOver6H == 1.00m &&
            setting.LunchDeductionOver4H == 0.25m));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowValidationException_AndNotPersistAnything_WhenRequestIsInvalid()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var branchesRepository = new BranchesRepositoryBuilder().Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var transactionTypesRepository = new TransactionTypesRepositoryBuilder().Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();

        var useCase = CreateUseCase(
            authenticationService,
            branchesRepository,
            branchUsersRepository,
            categoriesRepository,
            transactionTypesRepository,
            productsRepository,
            settingsRepository,
            unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
        await branchesRepository.DidNotReceive().Add(Arg.Any<Branch>());
        await branchUsersRepository.DidNotReceive().Add(Arg.Any<BranchUser>());
        await categoriesRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<Category>>());
        await transactionTypesRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<TransactionType>>());
        await productsRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<Product>>());
        await settingsRepository.DidNotReceive().Add(Arg.Any<Setting>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldNotCommit_WhenBranchPersistenceFails()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchJsonBuilder().Build();

        var branchesRepository = new BranchesRepositoryBuilder().Build();
        branchesRepository.Add(Arg.Any<Branch>()).ThrowsAsync(new InvalidOperationException("fail-error"));

        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var transactionTypesRepository = new TransactionTypesRepositoryBuilder().Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();

        var useCase = CreateUseCase(
            authenticationService,
            branchesRepository,
            branchUsersRepository,
            categoriesRepository,
            transactionTypesRepository,
            productsRepository,
            settingsRepository,
            unitOfWork);

        await Should.ThrowAsync<InvalidOperationException>(() => useCase.Execute(request));

        await branchUsersRepository.DidNotReceive().Add(Arg.Any<BranchUser>());
        await categoriesRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<Category>>());
        await transactionTypesRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<TransactionType>>());
        await productsRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<Product>>());
        await settingsRepository.DidNotReceive().Add(Arg.Any<Setting>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldNotCommit_WhenSeedInsertFails()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchJsonBuilder().Build();

        var branchesRepository = new BranchesRepositoryBuilder().Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        categoriesRepository.AddRange(Arg.Any<IEnumerable<Category>>())
            .ThrowsAsync(new InvalidOperationException("seed failure"));

        var transactionTypesRepository = new TransactionTypesRepositoryBuilder().Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();

        var useCase = CreateUseCase(
            authenticationService,
            branchesRepository,
            branchUsersRepository,
            categoriesRepository,
            transactionTypesRepository,
            productsRepository,
            settingsRepository,
            unitOfWork);

        await Should.ThrowAsync<InvalidOperationException>(() => useCase.Execute(request));

        await transactionTypesRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<TransactionType>>());
        await productsRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<Product>>());
        await settingsRepository.DidNotReceive().Add(Arg.Any<Setting>());
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateBranchUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IBranchesRepository branchesRepository,
        IBranchUsersRepository branchUsersRepository,
        ICategoriesRepository categoriesRepository,
        ITransactionTypesRepository transactionTypesRepository,
        IProductsRepository productsRepository,
        ISettingsRepository settingsRepository,
        IUnitOfWork unitOfWork)
    {
        return new CreateBranchUseCase(
            authenticationService,
            branchesRepository,
            branchUsersRepository,
            categoriesRepository,
            transactionTypesRepository,
            productsRepository,
            settingsRepository,
            unitOfWork);
    }
}
