using server.Application.Services.Transactions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Transactions;

public class BranchClockTest
{
    private readonly BranchClock _clock = new();

    [Fact]
    public void LocalBusinessDateTime_ShouldReturnFullSaoPauloDateTime_WhenUtcInstantIsAtMidnightBoundary()
    {
        // 2026-05-07T03:00:00Z = 2026-05-07T00:00:00 in São Paulo (UTC-3)
        var utcInstant = Utc(2026, 5, 7, 3, 0);

        var result = _clock.LocalBusinessDateTime(utcInstant);

        result.ShouldBe(new DateTime(2026, 5, 7, 0, 0, 0));
    }

    [Fact]
    public void LocalBusinessDateTime_ShouldReturnFullSaoPauloDateTime_WhenUtcInstantIsBeforeMidnightBoundary()
    {
        // 2026-05-07T02:00:00Z = 2026-05-06T23:00:00 in São Paulo (UTC-3)
        var utcInstant = Utc(2026, 5, 7, 2, 0);

        var result = _clock.LocalBusinessDateTime(utcInstant);

        result.ShouldBe(new DateTime(2026, 5, 6, 23, 0, 0));
    }

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
