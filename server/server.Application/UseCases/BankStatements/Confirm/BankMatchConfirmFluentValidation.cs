using FluentValidation;
using server.Application.Services.Banking;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.BankStatements.Confirm;

public class BankMatchConfirmFluentValidation : AbstractValidator<RequestBankMatchConfirmJson>
{
    public BankMatchConfirmFluentValidation()
    {
        RuleFor(r => r.PostedOn).InclusiveBetween(new DateTime(2000, 1, 1), new DateTime(2100, 12, 31)).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.Items).Must(items => items is { Count: > 0 and <= 200 } &&
            items.All(i => i is not null && i.ProposalId != Guid.Empty && i.LineId != Guid.Empty && i.ReceiptId != Guid.Empty && i.LineVersion > 0 && i.ReceiptVersion > 0 && i.Amount != 0 && i.ExpectationFingerprint is { Length: 64 }) &&
            items.Select(i => i.LineId).Distinct().Count() == items.Count && items.Select(i => i.ReceiptId).Distinct().Count() == items.Count && items.Select(i => i.ProposalId).Distinct().Count() == items.Count).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
    }
}
