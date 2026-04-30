using FluentValidation;
using server.Application.Services;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.DailyCloses.PutItems;

public class PutDailyCloseItemsFluentValidation : AbstractValidator<RequestPutDailyCloseItemsJson>
{
    public PutDailyCloseItemsFluentValidation()
    {
        RuleFor(r => r.Items)
            .NotNull()
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ITEMS_REQUIRED);

        When(r => r.Items is not null, () =>
        {
            RuleForEach(r => r.Items)
                .ChildRules(item =>
                {
                    item.RuleFor(i => i.ProductId)
                        .NotEqual(Guid.Empty)
                        .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_ID_REQUIRED);

                    item.RuleFor(i => i.Value)
                        .GreaterThanOrEqualTo(0m)
                        .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ITEM_VALUE_NEGATIVE);

                    item.RuleFor(i => i.Value)
                        .ValuePrecisionWithin14x2(ResourcesErrorMessages.DAILYCLOSE_ITEM_VALUE_PRECISION);
                });

            RuleFor(r => r.Items)
                .Must(items => items!.GroupBy(i => i.ProductId).All(g => g.Count() == 1))
                .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ITEM_DUPLICATE);
        });
    }
}
