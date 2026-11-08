using server.Domain.Entities;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.People;

internal static class PeopleHistory
{
    public static T? Previous<T>(IReadOnlyList<T> rows, Guid? id, uint? version, Func<T, Guid?> previous, Func<T, uint> getVersion) where T : EntityBase
    {
        if (id is null)
        {
            if (version is not null) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
            return null;
        }
        var row = rows.SingleOrDefault(r => r.Id == id) ?? throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
        if (version is null || getVersion(row) != version || rows.Any(r => previous(r) == id))
            throw new ConflictException(ResourcesErrorMessages.PEOPLE_STALE);
        return row;
    }
    public static void SourceExists<T>(IReadOnlyList<T> rows, Guid? id) where T : EntityBase
    {
        if (id != null && !rows.Any(r => r.Id == id)) throw new NotFoundException(ResourcesErrorMessages.PEOPLE_RECORD_NOT_FOUND);
    }
    public static IEnumerable<T> Heads<T>(IEnumerable<T> rows, Func<T, Guid?> previous) where T : EntityBase
    {
        var list = rows.ToList(); var replaced = list.Select(previous).OfType<Guid>().ToHashSet();
        return list.Where(r => !replaced.Contains(r.Id));
    }
    public static IReadOnlyList<T> Ancestors<T>(IReadOnlyList<T> rows, Guid id, Func<T, Guid?> previous) where T : EntityBase
    {
        var byId = rows.ToDictionary(r => r.Id);
        var chain = new List<T>();
        var visited = new HashSet<Guid>();
        while (true)
        {
            if (!visited.Add(id) || !byId.TryGetValue(id, out var row)) throw new ConflictException(ResourcesErrorMessages.PEOPLE_SOURCE_UNAVAILABLE);
            chain.Add(row);
            if (previous(row) is not { } parent) return chain;
            id = parent;
        }
    }
    public static void Require(bool condition)
    {
        if (!condition) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_SOURCE_UNAVAILABLE]);
    }
    public static void Open(DateTime date, DateTime lockedThrough)
    {
        if (date <= lockedThrough) throw new ConflictException(ResourcesErrorMessages.PEOPLE_PERIOD_LOCKED);
    }
}
