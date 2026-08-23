using server.Application.Services.Idempotency;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Idempotency;

public sealed class CanonicalJsonRequestHasherTest
{
    private readonly CanonicalJsonRequestHasher _hasher = new();

    [Fact]
    public void Compute_ShouldIgnoreObjectPropertyInsertionOrder()
    {
        var first = new Dictionary<string, object?>
        {
            ["date"] = "2026-08-20",
            ["value"] = 10.50m,
            ["description"] = null
        };
        var reordered = new Dictionary<string, object?>
        {
            ["description"] = null,
            ["value"] = 10.50m,
            ["date"] = "2026-08-20"
        };

        _hasher.Compute(first).ShouldBe(_hasher.Compute(reordered));
    }

    [Fact]
    public void Compute_ShouldNormalizeEquivalentDecimalRepresentations()
    {
        _hasher.Compute(new { Value = 10m }).ShouldBe(_hasher.Compute(new { Value = 10.000m }));
    }

    [Fact]
    public void Compute_ShouldPreserveArrayOrder()
    {
        _hasher.Compute(new { Values = new[] { 1, 2 } })
            .ShouldNotBe(_hasher.Compute(new { Values = new[] { 2, 1 } }));
    }
}
