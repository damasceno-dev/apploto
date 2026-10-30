using FluentValidation;
using server.Application.Services.Sessions;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Sessions;

internal static class SessionValidationExtensions
{
    internal const int ReasonMaxLength = SessionStarter.ReasonMaxLength;

    extension<T>(IRuleBuilder<T, string?> rule)
    {
        public IRuleBuilderOptions<T, string?> RequiredReason()
        {
            return rule
                .Must(reason => string.IsNullOrWhiteSpace(reason) is false)
                .WithMessage(ResourcesErrorMessages.SESSION_REASON_REQUIRED)
                .Must(reason => reason is null || reason.Length <= ReasonMaxLength)
                .WithMessage(ResourcesErrorMessages.SESSION_REASON_TOO_LONG);
        }

        public IRuleBuilderOptions<T, string?> OptionalReason()
        {
            return rule
                .Must(reason => reason is null || reason.Length <= ReasonMaxLength)
                .WithMessage(ResourcesErrorMessages.SESSION_REASON_TOO_LONG);
        }
    }

    extension<T>(IRuleBuilder<T, List<RequestSessionOpeningItemJson>?> rule)
    {
        public IRuleBuilderOptions<T, List<RequestSessionOpeningItemJson>?> OpeningItems()
        {
            return rule
                .Must(items => items is null || items.Select(item => item.ProductId).Distinct().Count() == items.Count)
                .WithMessage(ResourcesErrorMessages.SESSION_OPENING_ITEM_DUPLICATE)
                .Must(items => items is null || items.All(item => item.Value >= 0m))
                .WithMessage(ResourcesErrorMessages.SESSION_OPENING_VALUE_NEGATIVE)
                .Must(items => items is null || items.All(item => decimal.Round(item.Value, 2) == item.Value))
                .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ITEM_VALUE_PRECISION);
        }
    }
}
