using server.Application.Services.Reports;
using server.Domain.Entities.Enums;
using server.Domain.Models.Projections;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Reports;

public class CashVarianceFoldTest
{
    [Fact]
    public void From_ShouldFoldTotalsAndCountEachOfficialStatusApart()
    {
        VarianceTimeSeriesRow Row(decimal value, DailyCloseStatus status) =>
            new(new DateTime(2026, 10, 1), Guid.NewGuid(), "T1", value, status, Guid.NewGuid());

        // The overall max (9) and min (−7) sit inside multi-row groups, and no group's max equals its sum.
        var figures = CashVarianceFold.From([
            Row(1m, DailyCloseStatus.Submitted),
            Row(-7m, DailyCloseStatus.Approved),
            Row(-1m, DailyCloseStatus.Approved),
            Row(3m, DailyCloseStatus.Rejected),
            Row(2m, DailyCloseStatus.Rejected),
            Row(9m, DailyCloseStatus.Rejected)
        ]);

        figures.ShouldBe(new CashVarianceFigures(6, 7m, 7m / 6, 9m, -7m, 1, 2, 3));
    }

    [Fact]
    public void From_ShouldReturnZeros_WhenThereIsNoOfficialRow()
    {
        CashVarianceFold.From([]).ShouldBe(new CashVarianceFigures(0, 0m, 0m, 0m, 0m, 0, 0, 0));
    }
}
