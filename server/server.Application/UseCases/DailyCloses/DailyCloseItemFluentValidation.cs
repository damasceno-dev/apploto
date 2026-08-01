using FluentValidation;
using server.Application.Services;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.DailyCloses;

/// <summary>
/// Per-item rules shared by every slice that accepts a daily-close item payload — the
/// <c>PUT /items</c> write and the candidate variance preview. Lives at the slice root
/// rather than inside one operation folder because neither slice owns it.
/// </summary>
internal sealed class DailyCloseItemFluentValidation : AbstractValidator<RequestUpsertDailyCloseItemJson>
{
    public DailyCloseItemFluentValidation()
    {
        RuleFor(item => item.ProductId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_ID_REQUIRED);

        RuleFor(item => item.Value)
            .GreaterThanOrEqualTo(0m)
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ITEM_VALUE_NEGATIVE);

        RuleFor(item => item.Value)
            .ValuePrecisionWithin14x2(ResourcesErrorMessages.DAILYCLOSE_ITEM_VALUE_PRECISION);
    }
}
