using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Transactions.List;

public class ListTransactionsFluentValidation : AbstractValidator<RequestListTransactionsJson>
{
    internal const int MaximumPageSize = 200;

    public ListTransactionsFluentValidation()
    {
        RuleFor(r => r.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_LIST_PAGE_INVALID);

        RuleFor(r => r.PageSize)
            .InclusiveBetween(1, MaximumPageSize)
            .WithMessage(string.Format(ResourcesErrorMessages.TRANSACTION_LIST_PAGE_SIZE_INVALID, 1, MaximumPageSize));

        RuleFor(r => r.Status)
            .IsInEnum()
            .When(r => r.Status.HasValue)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_STATUS_INVALID);

        RuleFor(r => r)
            .Must(r => r.DateFrom is null || r.DateTo is null || r.DateFrom.Value <= r.DateTo.Value)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_LIST_DATE_RANGE_INVALID);

        RuleFor(r => r)
            .Must(r => r.Mine is false || r.OperatorId is null)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_LIST_MINE_AND_OPERATOR_ID_CONFLICT);
    }
}
