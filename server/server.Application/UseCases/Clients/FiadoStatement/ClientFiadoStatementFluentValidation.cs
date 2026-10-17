using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Clients.FiadoStatement;

internal class ClientFiadoStatementFluentValidation : AbstractValidator<RequestClientFiadoStatementJson>
{
    public const int MaxPageSize = 100;

    public ClientFiadoStatementFluentValidation()
    {
        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage(ResourcesErrorMessages.REPORT_PAGE_INVALID);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage(ResourcesErrorMessages.CLIENT_FIADO_STATEMENT_PAGE_SIZE_INVALID);

        When(request => request.DateFrom.HasValue, () =>
            RuleFor(request => request.DateFrom!.Value)
                .NotEqual(default(DateTime))
                .WithMessage(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID));

        When(request => request.DateTo.HasValue, () =>
            RuleFor(request => request.DateTo!.Value)
                .NotEqual(default(DateTime))
                .WithMessage(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID));

        When(request => request.DateFrom.HasValue && request.DateTo.HasValue, () =>
            RuleFor(request => request)
                .Must(request => request.DateFrom!.Value.Date <= request.DateTo!.Value.Date)
                .WithMessage(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED));
    }
}
