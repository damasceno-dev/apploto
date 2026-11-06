using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Sessions;

internal static class OpeningAttributionReader
{
    internal static async Task<OpeningDiscrepancyAttributionEvent?> Read(
        ISessionOpeningDiscrepanciesRepository repository, Guid discrepancyId, CancellationToken ct)
    {
        var heads = await repository.GetAttributionHeadsAsNoTracking(discrepancyId, ct);
        if (heads.Count > 1)
            throw new ConflictException(ResourcesErrorMessages.OPENING_DISCREPANCY_ATTRIBUTION_AMBIGUOUS);
        return heads.SingleOrDefault();
    }
}
