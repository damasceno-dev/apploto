using CommonTestUtilities.Entities;
using server.Application.Services.Settings;
using server.Domain.Entities.Enums;
using server.Domain.Models.Projections;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Settings;

public sealed class MonthLockReadinessEvaluatorTest
{
    private readonly MonthLockReadinessEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ShouldTreatCloseInAnyStateAsExpectedAndBlockWhenNotApproved()
    {
        var close = new DailyCloseBuilder().WithStatus(DailyCloseStatus.Draft).Build();

        var result = _evaluator.Evaluate([close], [], []);

        result.IsReady.ShouldBeFalse();
        result.UnapprovedCloses.ShouldHaveSingleItem().ShouldBeSameAs(close);
        result.MissingExpectedCloses.ShouldBeEmpty();
    }

    [Fact]
    public void Evaluate_ShouldRequireCloserForDirectTerminalActivity()
    {
        var activity = new TerminalActivityPairRow(
            new DateTime(2025, 5, 10), Guid.NewGuid(), "Terminal A");

        var result = _evaluator.Evaluate([], [], [activity]);

        result.IsReady.ShouldBeFalse();
        result.HasNoCloseMonthWithExpectedActivity.ShouldBeTrue();
        result.MissingExpectedCloses.ShouldHaveSingleItem().ShouldBe(activity);
    }

    [Fact]
    public void Evaluate_ShouldAcceptDirectActivityWhenItsCloseIsApproved()
    {
        var date = new DateTime(2025, 5, 12);
        var account = new AccountBuilder().WithName("Terminal A").Build();
        var close = new DailyCloseBuilder()
            .WithAccount(account)
            .WithDate(date)
            .WithStatus(DailyCloseStatus.Approved)
            .Build();
        var activity = new TerminalActivityPairRow(date, account.Id, account.Name);

        _evaluator.Evaluate([close], [], [activity]).IsReady.ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_ShouldApplyNoCalendarOrActivationWindowSpecialCase()
    {
        var saturday = new TerminalActivityPairRow(
            new DateTime(2025, 5, 10), Guid.NewGuid(), "Sábado");
        var holiday = new TerminalActivityPairRow(
            new DateTime(2025, 5, 1), Guid.NewGuid(), "Feriado");
        var outsideActivationWindow = new TerminalActivityPairRow(
            new DateTime(2025, 5, 20), Guid.NewGuid(), "Fora da janela");

        var result = _evaluator.Evaluate([], [], [saturday, holiday, outsideActivationWindow]);

        result.MissingExpectedCloses.ShouldBe([saturday, holiday, outsideActivationWindow], ignoreOrder: true);
    }

    [Fact]
    public void Evaluate_ShouldNotExpectTerminal_WhenOnlyPairedTabActivityExists()
    {
        // The repository contract supplies only direct Terminal pairs. A paired-Tab-only month
        // therefore reaches the evaluator with no Terminal activity pair and remains empty/ready.
        var result = _evaluator.Evaluate([], [], []);

        result.IsReady.ShouldBeTrue();
        result.HasNoCloseMonthWithExpectedActivity.ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_ShouldBlockDraftTransactionEvenWithoutExpectedPairs()
    {
        var counts = new[]
        {
            new MonthlyTransactionCountRow(
                new DateTime(2025, 5, 3), TransactionStatus.Draft, 1)
        };

        var result = _evaluator.Evaluate([], counts, []);

        result.IsReady.ShouldBeFalse();
        result.HasDraftTransactions.ShouldBeTrue();
    }
}
