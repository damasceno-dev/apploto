using server.Domain.Interfaces.Holidays;
using server.Domain.Models;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// Default external-provider fakes used in WebApi integration tests so the suite
/// never makes live HTTP calls to BrasilAPI or Nager.Date. Both implementations
/// return a failed <see cref="BrazilianHolidayProviderResult{T}"/>; in Composite
/// resolution this causes the resolver to fall back to the canonical calendar,
/// preserving the M6 Phase 6 test expectations of canonical-only output.
/// </summary>
internal sealed class UnavailableBrasilApiHolidayProvider : IBrasilApiHolidayProvider
{
    public Task<BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>> GetHolidaysForYear(
        int year,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>.Failure(
                "BrasilAPI is stubbed unavailable in WebApi integration tests"));
    }
}

internal sealed class UnavailableNagerDateHolidayProvider : INagerDateHolidayProvider
{
    public Task<BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>> GetHolidaysForYear(
        int year,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>.Failure(
                "Nager.Date is stubbed unavailable in WebApi integration tests"));
    }
}
