using FluentValidation;
using server.Application.UseCases.Branches.Create;
using server.Application.UseCases.Invitations.Issue;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Platform.OperateBranch;

public sealed class OperateBranchFluentValidation : AbstractValidator<RequestOperateBranchJson>
{
    public OperateBranchFluentValidation()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleFor(x => x.OwnerEmail).SetValidator(new OwnerEmailValidator());
        RuleFor(x => x.Name).NotEmpty().WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID)
            .MaximumLength(255).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleFor(x => x.Cnpj).MaximumLength(18).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleFor(x => x.Phone).MaximumLength(20).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleFor(x => x.Address).MaximumLength(255).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleFor(x => x.Terminals).NotNull().WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID)
            .Must(x => x is { Count: <= 30 }).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleFor(x => x.AdditionalProducts).NotNull().WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID)
            .Must(x => x is { Count: <= 30 }).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleFor(x => x.Operators).NotNull().WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID)
            .Must(x => x is { Count: <= 50 }).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleForEach(x => x.Terminals).Must(x => x is not null && !string.IsNullOrWhiteSpace(x.Name) && x.Name.Length <= 255 &&
            (x.PairedTabName is null || !string.IsNullOrWhiteSpace(x.PairedTabName) && x.PairedTabName.Length <= 255))
            .WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleForEach(x => x.AdditionalProducts).Must(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 255)
            .WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleForEach(x => x.Operators).Must(x => x is not null && !string.IsNullOrWhiteSpace(x.Name) && x.Name.Length <= 255 &&
            x.TerminalIndexes is { Count: <= 30 }).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        RuleFor(x => x).Must(x => ValidCollections(x)).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
    }

    private static bool ValidCollections(RequestOperateBranchJson x)
    {
        if (x.Terminals is null || x.AdditionalProducts is null || x.Operators is null ||
            x.Terminals.Any(t => t is null) || x.Operators.Any(o => o is null || o.TerminalIndexes is null)) return false;
        var names = CreateBranchSeedFactory.CreateDefaultProducts(Guid.Empty).Select(p => p.Name)
            .Concat(x.AdditionalProducts.Select(p => p?.Trim() ?? string.Empty));
        if (names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1)) return false;
        var accountNames = x.Terminals.SelectMany(t => new[] { t.Name.Trim(), t.PairedTabName?.Trim() }.OfType<string>());
        if (accountNames.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1)) return false;
        if (x.Operators.Select(o => o.Name.Trim()).GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1)) return false;
        return x.Operators.All(o => o.TerminalIndexes.Distinct().Count() == o.TerminalIndexes.Count &&
            o.TerminalIndexes.All(i => i >= 0 && i < x.Terminals.Count));
    }
    private sealed class OwnerEmailValidator : AbstractValidator<string>
    {
        public OwnerEmailValidator() => RuleFor(x => x).NotEmpty().WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID)
            .EmailAddress().WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID)
            .MaximumLength(255).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
    }
}
