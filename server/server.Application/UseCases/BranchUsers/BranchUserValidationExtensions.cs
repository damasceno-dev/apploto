using FluentValidation;
using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.UseCases.BranchUsers;

internal static class BranchUserValidationExtensions
{
    extension<T>(IRuleBuilder<T, Role?> rule)
    {
        public void ValidateBranchUserRole()
        {
            rule
                .NotNull()
                .WithMessage(ResourcesErrorMessages.ROLE_REQUIRED);

            rule
                .IsInEnum()
                .WithMessage(ResourcesErrorMessages.ROLE_INVALID);
        }
    }
}
