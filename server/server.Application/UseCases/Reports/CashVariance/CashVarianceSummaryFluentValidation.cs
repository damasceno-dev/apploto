using FluentValidation;
using server.Communication.Requests;

namespace server.Application.UseCases.Reports.CashVariance;

internal class CashVarianceSummaryFluentValidation : AbstractValidator<RequestCashVarianceSummaryJson>
{
    public CashVarianceSummaryFluentValidation()
    {
        RuleFor(r => r.DateFrom).DateRangeRequired();
        RuleFor(r => r.DateTo).DateRangeRequired();

        When(r => r.DateFrom != default && r.DateTo != default, () =>
        {
            RuleFor(r => r.DateFrom).DateRangeNotInverted(r => r.DateTo);
            RuleFor(r => r.DateFrom).DateRangeWithinCap(r => r.DateTo);
        });

        RuleFor(r => r.Page).PageBounds();
        RuleFor(r => r.PageSize).PageSizeBounds();
    }
}
