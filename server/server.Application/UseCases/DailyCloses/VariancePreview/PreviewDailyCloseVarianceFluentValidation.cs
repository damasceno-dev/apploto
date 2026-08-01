using FluentValidation;
using server.Communication.Requests;

namespace server.Application.UseCases.DailyCloses.VariancePreview;

public class PreviewDailyCloseVarianceFluentValidation : AbstractValidator<RequestDailyCloseVariancePreviewJson>
{
    public PreviewDailyCloseVarianceFluentValidation()
    {
        RuleFor(request => request.Items)
            .ValidateDailyCloseItems();

        When(request => request.Items is not null, () =>
        {
            RuleForEach(request => request.Items)
                .SetValidator(new DailyCloseItemFluentValidation());
        });
    }
}
