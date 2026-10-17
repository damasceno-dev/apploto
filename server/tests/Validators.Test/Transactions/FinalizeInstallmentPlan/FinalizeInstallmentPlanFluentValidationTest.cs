using CommonTestUtilities.Requests;
using server.Application.UseCases.Transactions.FinalizeInstallmentPlan;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Transactions.FinalizeInstallmentPlan;

public class FinalizeInstallmentPlanFluentValidationTest
{
    private readonly FinalizeInstallmentPlanFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestShapeIsValid()
    {
        var request = new RequestFinalizeInstallmentPlanJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenInstallmentsAreMissing()
    {
        var request = new RequestFinalizeInstallmentPlanJsonBuilder().WithInstallments(null).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENTS_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenInstallmentsAreEmpty()
    {
        var request = new RequestFinalizeInstallmentPlanJsonBuilder().WithInstallments([]).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENTS_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAnInstallmentIdIsEmpty()
    {
        var request = new RequestFinalizeInstallmentPlanJsonBuilder().WithInstallment(Guid.Empty, 5).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENT_ID_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAnInstallmentIdRepeats()
    {
        var repeated = Guid.NewGuid();
        var request = new RequestFinalizeInstallmentPlanJsonBuilder()
            .WithInstallments([])
            .WithInstallment(repeated, 5)
            .WithInstallment(repeated, 6)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENT_ID_INVALID);
    }

    [Fact]
    public void Validate_ShouldFailWithoutThrowing_WhenAnInstallmentEntryIsNull()
    {
        var request = new RequestFinalizeInstallmentPlanJsonBuilder().WithInstallments([null!]).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENT_ID_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAVersionIsZero()
    {
        var request = new RequestFinalizeInstallmentPlanJsonBuilder().WithInstallment(Guid.NewGuid(), 0).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENT_VERSION_INVALID);
    }
}
