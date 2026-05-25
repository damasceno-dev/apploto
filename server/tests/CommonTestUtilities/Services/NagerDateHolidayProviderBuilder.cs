using NSubstitute;
using server.Domain.Interfaces.Holidays;
using server.Domain.Models;

namespace CommonTestUtilities.Services;

/// <summary>
/// Builds an NSubstitute fake of <see cref="INagerDateHolidayProvider"/> for use in
/// unit and integration tests. The fake never throws — it returns
/// <see cref="BrazilianHolidayProviderResult{T}"/> values on every call. Throwing
/// from a provider fake would bubble through the resolver as a generic 500 instead
/// of the intended 502, masking the contract under test.
///
/// Default behavior (no configured year): returns
/// <see cref="BrazilianHolidayProviderResult{T}.Failure(string)"/> with a "no fixture"
/// reason, so tests that don't configure a specific year deterministically take the
/// canonical-backfill path.
/// </summary>
public class NagerDateHolidayProviderBuilder
{
    private readonly INagerDateHolidayProvider _provider = Substitute.For<INagerDateHolidayProvider>();

    public NagerDateHolidayProviderBuilder()
    {
        _provider
            .GetHolidaysForYear(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.Failure(
                    "No Nager.Date fixture configured for this year")));
    }

    public NagerDateHolidayProviderBuilder ReturnsSuccessForYear(int year, IReadOnlyList<NagerDateHolidayDto> dtos)
    {
        _provider
            .GetHolidaysForYear(year, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.SuccessResult(dtos)));
        return this;
    }

    public NagerDateHolidayProviderBuilder ReturnsFailureForYear(int year, string failureReason)
    {
        _provider
            .GetHolidaysForYear(year, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.Failure(failureReason)));
        return this;
    }

    public INagerDateHolidayProvider Build() => _provider;
}
