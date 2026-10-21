namespace server.Infrastructure.Repositories;

/// <summary>
/// LINQ <c>Skip</c> takes an <see cref="int"/>, so <c>(page - 1) * pageSize</c> overflows into a negative OFFSET for a
/// huge but valid page. Such a page lies beyond any result set the database can return, so callers answer it with an
/// empty page instead of an error (the whole-filter totals are unaffected).
/// </summary>
internal static class PageOffset
{
    public static bool TryGet(int page, int pageSize, out int offset)
    {
        var value = (long)(page - 1) * pageSize;
        offset = value <= int.MaxValue ? (int)value : 0;
        return value <= int.MaxValue;
    }
}
