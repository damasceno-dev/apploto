using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.DailyLedger;

internal class DailyLedgerFluentValidation : AbstractValidator<RequestDailyLedgerJson>
{
    public DailyLedgerFluentValidation()
    {
        RuleFor(r => r.AccountId)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.REPORT_ACCOUNT_ID_REQUIRED);

        RuleFor(r => r.DateFrom)
            .DateRangeRequired();

        RuleFor(r => r.DateTo)
            .DateRangeRequired();

        When(r => r.DateFrom != default && r.DateTo != default, () =>
        {
            RuleFor(r => r.DateFrom).DateRangeNotInverted(r => r.DateTo);
            RuleFor(r => r.DateFrom).DateRangeWithinCap(r => r.DateTo);
        });

        RuleFor(r => r.Page)
            .PageBounds();

        RuleFor(r => r.PageSize)
            .PageSizeBounds();
    }
}
