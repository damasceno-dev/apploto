using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Branches.Create;

public class CreateBranchFluentValidation : AbstractValidator<RequestCreateBranchJson>
{
    internal const int BranchNameMaxLength = 255;
    internal const int BranchCnpjMaxLength = 18;
    internal const int BranchPhoneMaxLength = 20;

    public CreateBranchFluentValidation()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.NAME_EMPTY);

        RuleFor(request => request.Name)
            .MaximumLength(BranchNameMaxLength)
            .WithMessage(string.Format(ResourcesErrorMessages.BRANCH_NAME_MAX_LENGTH, BranchNameMaxLength));

        RuleFor(request => request.Cnpj)
            .MaximumLength(BranchCnpjMaxLength)
            .When(request => string.IsNullOrWhiteSpace(request.Cnpj) is false)
            .WithMessage(string.Format(ResourcesErrorMessages.BRANCH_CNPJ_MAX_LENGTH, BranchCnpjMaxLength));

        RuleFor(request => request.Phone)
            .MaximumLength(BranchPhoneMaxLength)
            .When(request => string.IsNullOrWhiteSpace(request.Phone) is false)
            .WithMessage(string.Format(ResourcesErrorMessages.BRANCH_PHONE_MAX_LENGTH, BranchPhoneMaxLength));
    }
}
