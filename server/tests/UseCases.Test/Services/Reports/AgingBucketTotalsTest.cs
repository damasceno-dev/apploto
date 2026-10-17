using server.Application.Services.Reports;
using server.Domain.Entities.Enums;
using server.Domain.Models.Projections;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Reports;

public class AgingBucketTotalsTest
{
    private static readonly DateTime AsOf = new(2025, 6, 30);

    [Fact]
    public void Build_ShouldReturnEveryBucketInDeclaredOrder_WithZerosForEmptyOnes()
    {
        var totals = AgingBucketTotals.Build([], AsOf);

        totals.Select(total => total.Bucket).ShouldBe(Enum.GetValues<AgingBucket>());
        totals.ShouldAllBe(total => total.Value == 0m && total.RowCount == 0);
    }

    [Fact]
    public void Build_ShouldBucketEachDueDateAtTheAsOfDate_AndSumDatesSharingABucket()
    {
        var totals = AgingBucketTotals.Build(
        [
            new AgingDueDateTotalRow(AsOf.AddDays(5), 10m, 1),
            new AgingDueDateTotalRow(AsOf.AddDays(-30), 20m, 2),
            new AgingDueDateTotalRow(AsOf.AddDays(-1), 5m, 1),
            new AgingDueDateTotalRow(AsOf.AddDays(-31), 7m, 1),
            new AgingDueDateTotalRow(AsOf.AddDays(-91), 3m, 3)
        ], AsOf);

        totals.Single(t => t.Bucket == AgingBucket.Current).Value.ShouldBe(10m);
        totals.Single(t => t.Bucket == AgingBucket.Days0To30).Value.ShouldBe(25m);
        totals.Single(t => t.Bucket == AgingBucket.Days0To30).RowCount.ShouldBe(3);
        totals.Single(t => t.Bucket == AgingBucket.Days31To60).Value.ShouldBe(7m);
        totals.Single(t => t.Bucket == AgingBucket.Days61To90).Value.ShouldBe(0m);
        totals.Single(t => t.Bucket == AgingBucket.Days91Plus).RowCount.ShouldBe(3);
        totals.Sum(t => t.Value).ShouldBe(45m);
    }
}
