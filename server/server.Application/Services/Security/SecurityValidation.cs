using FluentValidation;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Security;

internal static class SecurityValidation
{
    public static void Validate<T>(IValidator<T> validator, T request)
    {
        var result = validator.Validate(request);
        if (!result.IsValid) throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
