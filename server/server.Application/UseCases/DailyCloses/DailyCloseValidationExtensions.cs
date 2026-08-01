using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.DailyCloses;

internal static class DailyCloseValidationExtensions
{
    internal const int NotesMaxLength = 1000;

    extension<T>(IRuleBuilder<T, IReadOnlyList<RequestUpsertDailyCloseItemJson>?> rule)
    {
        public void ValidateDailyCloseItems()
        {
            rule
                .NotNull()
                .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ITEMS_REQUIRED);
            rule
                .Must(items => items is null || items.GroupBy(item => item.ProductId).All(group => group.Count() == 1))
                .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ITEM_DUPLICATE);
        }
    }

    extension<T>(IRuleBuilder<T, string?> rule)
    {
        public void ValidateDailyCloseNotes()
        {
            rule
                .MaximumLength(NotesMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.DAILYCLOSE_NOTES_LENGTH, NotesMaxLength));
        }
    }
}
