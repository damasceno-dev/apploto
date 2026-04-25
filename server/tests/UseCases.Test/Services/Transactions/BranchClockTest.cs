using server.Application.Services.Transactions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Transactions;

public class BranchClockTest
{
    private readonly BranchClock _clock = new();

    [Fact]
    public void LocalBusinessDate_ShouldUseSaoPauloDate_WhenUtcInstantIsBeforeLocalMidnightBoundary()
    {
        var utcInstant = Utc(2026, 4, 25, 2, 59);

        var businessDate = _clock.LocalBusinessDate(utcInstant);

        businessDate.ShouldBe(new DateTime(2026, 4, 24));
    }

    [Fact]
    public void LocalBusinessDate_ShouldUseSaoPauloDate_WhenUtcInstantReachesLocalMidnightBoundary()
    {
        var utcInstant = Utc(2026, 4, 25, 3, 0);

        var businessDate = _clock.LocalBusinessDate(utcInstant);

        businessDate.ShouldBe(new DateTime(2026, 4, 25));
    }

    [Theory]
    [InlineData("2026-04-24", "2026-04-25T02:59:00Z", true)]
    [InlineData("2026-04-25", "2026-04-25T02:59:00Z", false)]
    [InlineData("2026-04-25", "2026-04-25T03:00:00Z", true)]
    public void IsSameLocalDay_ShouldCompareAgainstSaoPauloBusinessDate(
        string localBusinessDateText,
        string utcInstantText,
        bool expected)
    {
        var localBusinessDate = DateTime.Parse(localBusinessDateText);
        var utcInstant = DateTime.Parse(
            utcInstantText,
            null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

        var result = _clock.IsSameLocalDay(localBusinessDate, utcInstant);

        result.ShouldBe(expected);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }
}
