using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.TransactionTypes;

internal static class ReclassificationValidationExtensions
{
    internal const int MaxWindowDays = 366;
    internal const int MaxRows = 500;
    internal const int ReasonMaxLength = 500;

    extension<T>(IRuleBuilder<T, DateTime> rule)
    {
        public void ReclassificationWindowStart(Func<T, DateTime> dateTo)
        {
            rule
                .Must((instance, from) =>
                    from != default && dateTo(instance) != default && from.Date <= dateTo(instance).Date)
                .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_WINDOW_INVALID)
                .Must((instance, from) => (dateTo(instance).Date - from.Date).Days < MaxWindowDays)
                .WithMessage(string.Format(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_WINDOW_TOO_WIDE, MaxWindowDays));
        }
    }
}
