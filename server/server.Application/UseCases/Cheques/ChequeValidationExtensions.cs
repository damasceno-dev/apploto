using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Cheques;

internal static class ChequeValidationExtensions
{
    internal const int MaxItems = 200;
    internal const int NextDepositMaxDays = 366;
    internal const int ReturnReasonMaxLength = 100;
    internal const int DescriptionMaxLength = 500;

    private static readonly DateTime MinDate = new(2000, 1, 1);
    private static readonly DateTime MaxDate = new(2100, 12, 31);

    extension<T>(IRuleBuilder<T, DateTime> rule)
    {
        /// <summary>A required event date inside the supported range (relation to today is checked by the command).</summary>
        public void SettlementOccurredOn()
        {
            rule
                .NotEqual(default(DateTime))
                .WithMessage(ResourcesErrorMessages.SETTLEMENT_OCCURRED_ON_REQUIRED)
                .Must(date => date == default || (date.Date >= MinDate && date.Date <= MaxDate))
                .WithMessage(ResourcesErrorMessages.SETTLEMENT_DATE_OUT_OF_RANGE);
        }
    }

    extension<T>(IRuleBuilder<T, DateTime?> rule)
    {
        /// <summary>An optional posting date: in range and never before the event's own date.</summary>
        public void SettlementPostedOn(Func<T, DateTime> occurredOn)
        {
            rule
                .Must(date => date is null || (date.Value.Date >= MinDate && date.Value.Date <= MaxDate))
                .WithMessage(ResourcesErrorMessages.SETTLEMENT_DATE_OUT_OF_RANGE)
                .Must((instance, date) => date is null || date.Value.Date >= occurredOn(instance).Date)
                .WithMessage(ResourcesErrorMessages.SETTLEMENT_POSTED_ON_BEFORE_OCCURRED_ON);
        }
    }

    extension<T>(IRuleBuilder<T, int> rule)
    {
        public void ChequeExpectedVersion()
        {
            rule
                .GreaterThanOrEqualTo(0)
                .WithMessage(ResourcesErrorMessages.CHEQUE_EXPECTED_VERSION_INVALID);
        }
    }

    extension<T>(IRuleBuilder<T, List<RequestChequeOperationItemJson>> rule)
    {
        /// <summary>A non-empty, bounded list of distinct cheques, each with a valid expected version.</summary>
        public void ChequeItems()
        {
            rule
                .NotEmpty()
                .WithMessage(ResourcesErrorMessages.CHEQUE_ITEMS_REQUIRED)
                .Must(items => items.Count <= MaxItems)
                .WithMessage(string.Format(ResourcesErrorMessages.CHEQUE_ITEMS_MAX_COUNT, MaxItems))
                .Must(items => items.All(item => item.TransactionId != Guid.Empty))
                .WithMessage(ResourcesErrorMessages.CHEQUE_ITEM_ID_REQUIRED)
                .Must(items => items.All(item => item.ExpectedVersion >= 0))
                .WithMessage(ResourcesErrorMessages.CHEQUE_EXPECTED_VERSION_INVALID)
                .Must(items => items.Select(item => item.TransactionId).Distinct().Count() == items.Count)
                .WithMessage(ResourcesErrorMessages.CHEQUE_ITEMS_DUPLICATE);
        }
    }
}
