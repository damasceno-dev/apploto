using server.Application.UseCases.Clients.FiadoStatement;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Clients.FiadoStatement;

public class ClientFiadoStatementFluentValidationTest
{
    private readonly ClientFiadoStatementFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WithDefaultsAndNoWindow()
    {
        _validator.Validate(new RequestClientFiadoStatementJson()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Validate_ShouldSucceed_AtThePageSizeBounds(int pageSize)
    {
        _validator.Validate(new RequestClientFiadoStatementJson { PageSize = pageSize }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_ShouldFail_OutsideThePageSizeBounds(int pageSize)
    {
        var result = _validator.Validate(new RequestClientFiadoStatementJson { PageSize = pageSize });

        result.Errors.ShouldHaveSingleItem().ErrorMessage
            .ShouldBe(ResourcesErrorMessages.CLIENT_FIADO_STATEMENT_PAGE_SIZE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsBelowOne()
    {
        var result = _validator.Validate(new RequestClientFiadoStatementJson { Page = 0 });

        result.Errors.ShouldHaveSingleItem().ErrorMessage.ShouldBe(ResourcesErrorMessages.REPORT_PAGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldAcceptASingleDayWindow_AndRejectAnInvertedOne()
    {
        var sameDay = new RequestClientFiadoStatementJson { DateFrom = new DateTime(2025, 6, 1), DateTo = new DateTime(2025, 6, 1) };
        var inverted = new RequestClientFiadoStatementJson { DateFrom = new DateTime(2025, 6, 2), DateTo = new DateTime(2025, 6, 1) };

        _validator.Validate(sameDay).IsValid.ShouldBeTrue();
        _validator.Validate(inverted).Errors.ShouldHaveSingleItem().ErrorMessage
            .ShouldBe(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
    }

    [Fact]
    public void Validate_ShouldAcceptAnOpenEndedWindow_OnEitherSide()
    {
        _validator.Validate(new RequestClientFiadoStatementJson { DateFrom = new DateTime(2025, 6, 1) }).IsValid.ShouldBeTrue();
        _validator.Validate(new RequestClientFiadoStatementJson { DateTo = new DateTime(2025, 6, 1) }).IsValid.ShouldBeTrue();
    }
}
