using FluentValidation;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.UseCases.Reports.UpcomingSettlements;

internal class UpcomingSettlementsFluentValidation : AbstractValidator<RequestUpcomingSettlementsJson>
{
    public UpcomingSettlementsFluentValidation()
    {
        RuleFor(r => r.DueDateFrom).DateRangeRequired();
        RuleFor(r => r.DueDateTo).DateRangeRequired();

        When(r => r.DueDateFrom != default && r.DueDateTo != default, () =>
        {
            RuleFor(r => r.DueDateFrom).DateRangeNotInverted(r => r.DueDateTo);
            RuleFor(r => r.DueDateFrom).DateRangeWithinCap(r => r.DueDateTo);
        });

        When(r => r.Instrument.HasValue, () =>
        {
            RuleFor(r => r.Instrument)
                .IsInEnum()
                .WithMessage(ResourcesErrorMessages.REPORT_SETTLEMENT_INSTRUMENT_INVALID)
                .NotEqual(SettlementInstrument.None)
                .WithMessage(ResourcesErrorMessages.REPORT_SETTLEMENT_INSTRUMENT_INVALID);
        });

        RuleFor(r => r.Page).PageBounds();
        RuleFor(r => r.PageSize).PageSizeBounds();
    }
}
