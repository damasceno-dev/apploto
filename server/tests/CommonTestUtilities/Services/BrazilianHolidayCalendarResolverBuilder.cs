using NSubstitute;
using server.Application.Services.Holidays;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Services;

public class BrazilianHolidayCalendarResolverBuilder
{
    private readonly IBrazilianHolidayCalendarResolver _resolver = Substitute.For<IBrazilianHolidayCalendarResolver>();

    public BrazilianHolidayCalendarResolverBuilder()
    {
        // By default, delegate to the canonical calendar (mirrors prior canonical-only behavior).
        var canonicalCalendar = new BrazilianHolidayCalendar();

        _resolver
            .GetForYear(
                Arg.Any<int>(),
                Arg.Any<bool>(),
                Arg.Any<BrazilianHolidayCalendarSource>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var year = call.ArgAt<int>(0);
                var includeOptionalFederal = call.ArgAt<bool>(1);

                return Task.FromResult<IReadOnlyList<SourcedBrazilianHolidayEntry>>(
                    canonicalCalendar
                        .GetForYear(year, includeOptionalFederal)
                        .Select(entry => new SourcedBrazilianHolidayEntry(
                            entry.Date,
                            entry.Description,
                            entry.Type,
                            HolidaySource.Canonical))
                        .ToList());
            });
    }

    public BrazilianHolidayCalendarResolverBuilder Returns(
        int year,
        bool includeOptionalFederal,
        BrazilianHolidayCalendarSource source,
        IReadOnlyList<SourcedBrazilianHolidayEntry> entries)
    {
        _resolver
            .GetForYear(year, includeOptionalFederal, source, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(entries));
        return this;
    }

    public IBrazilianHolidayCalendarResolver Build() => _resolver;
}
