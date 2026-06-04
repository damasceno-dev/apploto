using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Reports;
using server.Application.UseCases.Transactions.EditPreview;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Reports;

/// <summary>
/// Direct coverage of the four impact sections the edit-preview use case delegates to the
/// projector. The use-case test substitutes this projector; the WebApi suite exercises it
/// against a real database. These unit tests pin the branch logic of each section.
/// </summary>
public class TransactionEditImpactProjectorTest
{
    private static readonly DateTime AsOfDate = new(2025, 2, 1);
    private static readonly Guid BranchId = Guid.NewGuid();

    // ---- ReceivableImpact ----

    [Fact]
    public async Task Project_ShouldComputeBeforeAndAfterBuckets_WhenUnpaidDueDateShiftsAcrossBucket()
    {
        var edit = BuildEdit(
            currentPaidAt: null,
            hypotheticalPaidAt: null,
            currentDueDate: AsOfDate.AddDays(-31),
            hypotheticalDueDate: AsOfDate);
        var projector = CreateProjector();

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.ReceivableImpact.BucketBefore.ShouldBe(AgingBucket.Days31To60);
        impact.ReceivableImpact.BucketAfter.ShouldBe(AgingBucket.Days0To30);
        impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeFalse();
        impact.ReceivableImpact.RowDisappearsFromOpenReceivables.ShouldBeFalse();
    }

    [Fact]
    public async Task Project_ShouldMarkRowDisappearing_WhenPaidAtFlipsFromNullToSet()
    {
        var edit = BuildEdit(
            currentPaidAt: null,
            hypotheticalPaidAt: AsOfDate,
            currentDueDate: AsOfDate.AddDays(-10),
            hypotheticalDueDate: AsOfDate.AddDays(-10));
        var projector = CreateProjector();

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.ReceivableImpact.BucketBefore.ShouldBe(AgingBucket.Days0To30);
        impact.ReceivableImpact.BucketAfter.ShouldBeNull();
        impact.ReceivableImpact.RowDisappearsFromOpenReceivables.ShouldBeTrue();
        impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeFalse();
    }

    [Fact]
    public async Task Project_ShouldMarkRowAppearing_WhenPaidAtFlipsFromSetToNull()
    {
        var edit = BuildEdit(
            currentPaidAt: AsOfDate.AddDays(-1),
            hypotheticalPaidAt: null,
            currentDueDate: AsOfDate.AddDays(-95),
            hypotheticalDueDate: AsOfDate.AddDays(-95));
        var projector = CreateProjector();

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.ReceivableImpact.BucketBefore.ShouldBeNull();
        impact.ReceivableImpact.BucketAfter.ShouldBe(AgingBucket.Days91Plus);
        impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeTrue();
        impact.ReceivableImpact.RowDisappearsFromOpenReceivables.ShouldBeFalse();
    }

    [Fact]
    public async Task Project_ShouldLeaveReceivableImpactEmpty_WhenBothPaidAtAreSet()
    {
        var edit = BuildEdit(
            currentPaidAt: AsOfDate.AddDays(-2),
            hypotheticalPaidAt: AsOfDate.AddDays(-1),
            currentDueDate: AsOfDate.AddDays(-10),
            hypotheticalDueDate: AsOfDate.AddDays(-20));
        var projector = CreateProjector();

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.ReceivableImpact.BucketBefore.ShouldBeNull();
        impact.ReceivableImpact.BucketAfter.ShouldBeNull();
        impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeFalse();
        impact.ReceivableImpact.RowDisappearsFromOpenReceivables.ShouldBeFalse();
    }

    // ---- FiadoBalanceImpact ----

    [Fact]
    public async Task Project_ShouldMoveOutstandingBetweenClients_WhenTabClientReassigned()
    {
        var oldClientId = Guid.NewGuid();
        var newClientId = Guid.NewGuid();
        var edit = BuildEdit(
            accountType: AccountType.Tab,
            direction: Direction.Out,
            value: 150m,
            currentClientId: oldClientId,
            hypotheticalClientId: newClientId);

        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(oldClientId, BranchId, ClientNamed(oldClientId, "Old Client"))
            .GetActiveByIdAndBranchIdAsNoTracking(newClientId, BranchId, ClientNamed(newClientId, "New Client"))
            .Build();
        var projector = CreateProjector(clientsRepository: clientsRepository);

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.FiadoBalanceImpact.Deltas.Count.ShouldBe(2);

        var oldDelta = impact.FiadoBalanceImpact.Deltas.Single(d => d.ClientId == oldClientId);
        oldDelta.OutstandingDelta.ShouldBe(-150m);
        oldDelta.ClientName.ShouldBe("Old Client");

        var newDelta = impact.FiadoBalanceImpact.Deltas.Single(d => d.ClientId == newClientId);
        newDelta.OutstandingDelta.ShouldBe(150m);
        newDelta.ClientName.ShouldBe("New Client");
    }

    [Fact]
    public async Task Project_ShouldUseInDirectionSign_WhenReassigningInDirectionTabRow()
    {
        var oldClientId = Guid.NewGuid();
        var newClientId = Guid.NewGuid();
        var edit = BuildEdit(
            accountType: AccountType.Tab,
            direction: Direction.In,
            value: 80m,
            currentClientId: oldClientId,
            hypotheticalClientId: newClientId);

        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(oldClientId, BranchId, ClientNamed(oldClientId, "Old"))
            .GetActiveByIdAndBranchIdAsNoTracking(newClientId, BranchId, ClientNamed(newClientId, "New"))
            .Build();
        var projector = CreateProjector(clientsRepository: clientsRepository);

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        // §6.4: In lowers outstanding (signedValue = −Value). Old client loses the negative
        // (+80), new client gains it (−80).
        impact.FiadoBalanceImpact.Deltas.Single(d => d.ClientId == oldClientId).OutstandingDelta.ShouldBe(80m);
        impact.FiadoBalanceImpact.Deltas.Single(d => d.ClientId == newClientId).OutstandingDelta.ShouldBe(-80m);
    }

    [Fact]
    public async Task Project_ShouldEmitSingleDelta_WhenAssigningClientWhereNoneExisted()
    {
        var newClientId = Guid.NewGuid();
        var edit = BuildEdit(
            accountType: AccountType.Tab,
            direction: Direction.Out,
            value: 50m,
            currentClientId: null,
            hypotheticalClientId: newClientId);

        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(newClientId, BranchId, ClientNamed(newClientId, "Assigned"))
            .Build();
        var projector = CreateProjector(clientsRepository: clientsRepository);

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.FiadoBalanceImpact.Deltas.Count.ShouldBe(1);
        impact.FiadoBalanceImpact.Deltas[0].ClientId.ShouldBe(newClientId);
        impact.FiadoBalanceImpact.Deltas[0].OutstandingDelta.ShouldBe(50m);
    }

    [Fact]
    public async Task Project_ShouldLeaveFiadoImpactEmpty_WhenAccountIsNotTab()
    {
        var edit = BuildEdit(
            accountType: AccountType.Terminal,
            currentClientId: Guid.NewGuid(),
            hypotheticalClientId: Guid.NewGuid());
        var projector = CreateProjector();

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.FiadoBalanceImpact.Deltas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Project_ShouldLeaveFiadoImpactEmpty_WhenTabClientUnchanged()
    {
        var clientId = Guid.NewGuid();
        var edit = BuildEdit(
            accountType: AccountType.Tab,
            currentClientId: clientId,
            hypotheticalClientId: clientId);
        var projector = CreateProjector();

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.FiadoBalanceImpact.Deltas.ShouldBeEmpty();
    }

    // ---- CashVarianceImpact ----

    [Theory]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Approved)]
    [InlineData(DailyCloseStatus.Rejected)]
    public async Task Project_ShouldLiveRecomputeCurrentVariance_WhenCloseHasCommittedCounts(
        DailyCloseStatus status)
    {
        // Submitted (pending), Approved (signed-off), and Rejected (repudiated) all retain a complete
        // submitted closing-count set, so §6.12 yields a real number for each; DailyCloseStatus tells
        // the caller which kind it is. Only Draft (counts still moving) withholds the number.
        var edit = BuildEdit();
        var dailyClose = CloseWithStatus(edit.AccountId, edit.Date, status);
        var dailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByBranchIdAndAccountIdAndDateAsNoTrackingReturns(BranchId, edit.AccountId, edit.Date, dailyClose)
            .Build();

        var cashVarianceProductId = Guid.NewGuid();
        var resolver = Substitute.For<ICashVarianceProductResolver>();
        resolver.GetIdAsync(BranchId, Arg.Any<CancellationToken>()).Returns(cashVarianceProductId);

        var calculator = Substitute.For<ICashVarianceCalculator>();
        calculator
            .CalculateAsync(BranchId, edit.AccountId, edit.Date, dailyClose.Id, cashVarianceProductId, Arg.Any<CancellationToken>())
            .Returns(50m);

        var projector = CreateProjector(
            dailyClosesRepository: dailyClosesRepository,
            cashVarianceCalculator: calculator,
            cashVarianceProductResolver: resolver);

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(status);
        impact.CashVarianceImpact.AccountId.ShouldBe(edit.AccountId);
        impact.CashVarianceImpact.Date.ShouldBe(edit.Date);
        impact.CashVarianceImpact.CurrentVariance.ShouldBe(50m);
        impact.CashVarianceImpact.ProjectedVariance.ShouldBe(50m);
        // §6.12 is blind to every editable field, so an edit cannot move the variance.
        impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
    }

    [Fact]
    public async Task Project_ShouldReportOpenButWithholdVariance_WhenCloseIsDraft()
    {
        var edit = BuildEdit();
        var dailyClose = CloseWithStatus(edit.AccountId, edit.Date, DailyCloseStatus.Draft);
        var dailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByBranchIdAndAccountIdAndDateAsNoTrackingReturns(BranchId, edit.AccountId, edit.Date, dailyClose)
            .Build();
        var projector = CreateProjector(dailyClosesRepository: dailyClosesRepository);

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(DailyCloseStatus.Draft);
        impact.CashVarianceImpact.AccountId.ShouldBe(edit.AccountId);
        impact.CashVarianceImpact.Date.ShouldBe(edit.Date);
        // Draft counts are still moving — surface the open close + delta but withhold a number.
        impact.CashVarianceImpact.CurrentVariance.ShouldBeNull();
        impact.CashVarianceImpact.ProjectedVariance.ShouldBeNull();
        impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
    }

    [Fact]
    public async Task Project_ShouldReportClosedVariance_WhenNoDailyCloseExistsForDate()
    {
        var edit = BuildEdit();
        var projector = CreateProjector();

        var impact = await projector.Project(edit, AsOfDate, BranchId);

        impact.CashVarianceImpact.DailyCloseStatus.ShouldBeNull();
        impact.CashVarianceImpact.AccountId.ShouldBeNull();
        impact.CashVarianceImpact.Date.ShouldBeNull();
        impact.CashVarianceImpact.CurrentVariance.ShouldBeNull();
        impact.CashVarianceImpact.ProjectedVariance.ShouldBeNull();
        impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
    }

    private static TransactionEditImpactProjector CreateProjector(
        IDailyClosesRepository? dailyClosesRepository = null,
        IClientsRepository? clientsRepository = null,
        ICashVarianceCalculator? cashVarianceCalculator = null,
        ICashVarianceProductResolver? cashVarianceProductResolver = null)
    {
        return new TransactionEditImpactProjector(
            dailyClosesRepository ?? new DailyClosesRepositoryBuilder().Build(),
            clientsRepository ?? new ClientsRepositoryBuilder().Build(),
            cashVarianceCalculator ?? Substitute.For<ICashVarianceCalculator>(),
            cashVarianceProductResolver ?? Substitute.For<ICashVarianceProductResolver>());
    }

    private static Client ClientNamed(Guid id, string name)
    {
        return new ClientBuilder().WithId(id).WithBranchId(BranchId).WithName(name).Build();
    }

    private static DailyClose CloseWithStatus(Guid accountId, DateTime date, DailyCloseStatus status)
    {
        return new DailyClose
        {
            Id = Guid.NewGuid(),
            BranchId = BranchId,
            AccountId = accountId,
            Date = date,
            Status = status
        };
    }

    private static HypotheticalTransactionEdit BuildEdit(
        AccountType accountType = AccountType.Tab,
        Direction direction = Direction.Out,
        decimal value = 150m,
        DateTime? date = null,
        TransactionStatus status = TransactionStatus.Active,
        Guid? currentClientId = null,
        Guid? hypotheticalClientId = null,
        DateTime? currentDueDate = null,
        DateTime? hypotheticalDueDate = null,
        DateTime? currentPaidAt = null,
        DateTime? hypotheticalPaidAt = null)
    {
        var transactionDate = date ?? new DateTime(2025, 1, 1);
        var branch = new BranchBuilder().WithId(BranchId).Build();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Cat",
            DefaultDirection = direction,
            BranchId = branch.Id,
            Branch = branch
        };
        var transactionType = new TransactionTypeBuilder().WithCategory(category).Build();
        var account = new AccountBuilder().WithType(accountType).WithBranch(branch).Build();

        var snapshot = new TransactionBuilder()
            .WithBranch(branch)
            .WithAccount(account)
            .WithTransactionType(transactionType)
            .WithValue(value)
            .WithDate(transactionDate)
            .WithStatus(status)
            .WithClientId(currentClientId)
            .WithDueDate(currentDueDate ?? transactionDate)
            .WithPaidAt(currentPaidAt)
            .Build();

        var payload = new RequestUpdateTransactionJson
        {
            Description = "hypothetical",
            DueDate = hypotheticalDueDate ?? snapshot.DueDate,
            PaidAt = hypotheticalPaidAt,
            ClientId = hypotheticalClientId,
            TransactionTime = new TimeOnly(12, 0)
        };

        return new HypotheticalTransactionEdit(snapshot, payload);
    }
}
