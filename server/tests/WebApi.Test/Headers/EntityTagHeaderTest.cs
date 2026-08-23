using server.Exceptions;
using server.Exceptions.Exceptions;
using server.Headers;
using Shouldly;
using Xunit;

namespace WebApi.Test.Headers;

public sealed class EntityTagHeaderTest
{
    [Fact]
    public void Format_ShouldReturnStrongQuotedDecimalXmin()
    {
        EntityTagHeader.Format(123u).ShouldBe("\"123\"");
    }

    [Fact]
    public void ParseRequired_ShouldReadStrongQuotedDecimalXmin()
    {
        EntityTagHeader.ParseRequired("\"4294967295\"").ShouldBe(uint.MaxValue);
    }

    [Fact]
    public void ParseRequired_ShouldReturnExactRequiredKey_WhenHeaderIsMissing()
    {
        var exception = Should.Throw<OnValidationException>(() => EntityTagHeader.ParseRequired(null));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("W/\"123\"")]
    [InlineData("\"0\"")]
    [InlineData("\"123\", \"124\"")]
    [InlineData("\"not-a-number\"")]
    public void ParseRequired_ShouldReturnExactInvalidKey_WhenHeaderIsNotOneStrongPositiveXmin(string value)
    {
        var exception = Should.Throw<OnValidationException>(() => EntityTagHeader.ParseRequired(value));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CONCURRENCY_IF_MATCH_INVALID);
    }
}
