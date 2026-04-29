namespace server.Application.Services.DailyCloses;

public interface ICashVarianceProductResolver
{
    /// <summary>
    /// Returns the id of the seeded cash-variance product for the given branch.
    /// Throws <see cref="InvalidOperationException"/> if the seed is missing; that is a
    /// bootstrap defect, not a recoverable runtime condition.
    /// </summary>
    Task<Guid> GetIdAsync(Guid branchId, CancellationToken ct = default);
}
