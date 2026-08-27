using CommonTestUtilities.Requests;
using server.Application.UseCases.Transactions.List;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Transactions.List;

public class ListTransactionsFluentValidationTest
{
    private readonly ListTransactionsFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenDefaultRequest()
    {
        var request = new RequestListTransactionsJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsZero()
    {
        var request = new RequestListTransactionsJsonBuilder()
            .WithPage(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_LIST_PAGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeIsZero()
    {
        var request = new RequestListTransactionsJsonBuilder()
            .WithPageSize(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.TRANSACTION_LIST_PAGE_SIZE_INVALID, 1, ListTransactionsFluentValidation.MaximumPageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeIsOverTheCap()
    {
        var request = new RequestListTransactionsJsonBuilder()
            .WithPageSize(ListTransactionsFluentValidation.MaximumPageSize + 1)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.TRANSACTION_LIST_PAGE_SIZE_INVALID, 1, ListTransactionsFluentValidation.MaximumPageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenStatusIsUndefined()
    {
        var request = new RequestListTransactionsJsonBuilder()
            .WithStatus((TransactionStatus)999)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_STATUS_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateFromIsAfterDateTo()
    {
        var request = new RequestListTransactionsJsonBuilder()
            .WithDateFrom(new DateTime(2025, 3, 11))
            .WithDateTo(new DateTime(2025, 3, 10))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_LIST_DATE_RANGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenMineAndOperatorIdAreCombined()
    {
        var request = new RequestListTransactionsJsonBuilder()
            .WithMine(true)
            .WithOperatorId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_LIST_MINE_AND_OPERATOR_ID_CONFLICT);
    }
}
