using server.Application.Services.Reports;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Reports;

public class ReportAgingBucketizerTest
{
    private static readonly DateTime AsOf = new(2024, 6, 1);

    [Fact]
    public void BucketFor_FutureDueDate_ReturnsCurrent()
    {
        var result = ReportAgingBucketizer.BucketFor(AsOf.AddDays(1), AsOf);

        result.ShouldBe(AgingBucket.Current);
    }

    [Fact]
    public void BucketFor_SameDayDueDateLaterTime_ReturnsDays0To30()
    {
        // dueDate is later in the same calendar day — must not be treated as future-due
        var result = ReportAgingBucketizer.BucketFor(AsOf.AddHours(5), AsOf);

        result.ShouldBe(AgingBucket.Days0To30);
    }

    [Fact]
    public void BucketFor_Day0_ReturnsDays0To30()
    {
        var result = ReportAgingBucketizer.BucketFor(AsOf, AsOf);

        result.ShouldBe(AgingBucket.Days0To30);
    }

    [Fact]
    public void BucketFor_Day30_ReturnsDays0To30()
    {
        var result = ReportAgingBucketizer.BucketFor(AsOf.AddDays(-30), AsOf);

        result.ShouldBe(AgingBucket.Days0To30);
    }

    [Fact]
    public void BucketFor_Day31_ReturnsDays31To60()
    {
        var result = ReportAgingBucketizer.BucketFor(AsOf.AddDays(-31), AsOf);

        result.ShouldBe(AgingBucket.Days31To60);
    }

    [Fact]
    public void BucketFor_Day60_ReturnsDays31To60()
    {
        var result = ReportAgingBucketizer.BucketFor(AsOf.AddDays(-60), AsOf);

        result.ShouldBe(AgingBucket.Days31To60);
    }

    [Fact]
    public void BucketFor_Day61_ReturnsDays61To90()
    {
        var result = ReportAgingBucketizer.BucketFor(AsOf.AddDays(-61), AsOf);

        result.ShouldBe(AgingBucket.Days61To90);
    }

    [Fact]
    public void BucketFor_Day90_ReturnsDays61To90()
    {
        var result = ReportAgingBucketizer.BucketFor(AsOf.AddDays(-90), AsOf);

        result.ShouldBe(AgingBucket.Days61To90);
    }

    [Fact]
    public void BucketFor_Day91_ReturnsDays91Plus()
    {
        var result = ReportAgingBucketizer.BucketFor(AsOf.AddDays(-91), AsOf);

        result.ShouldBe(AgingBucket.Days91Plus);
    }

    [Fact]
    public void BucketFor_VeryFarPast_ReturnsDays91Plus()
    {
        var result = ReportAgingBucketizer.BucketFor(AsOf.AddDays(-500), AsOf);

        result.ShouldBe(AgingBucket.Days91Plus);
    }
}
