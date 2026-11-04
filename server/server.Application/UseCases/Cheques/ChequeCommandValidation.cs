using FluentValidation;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Cheques;

internal static class ChequeCommandValidation
{
    public static void Validate<TRequest>(AbstractValidator<TRequest> validator, TRequest request)
    {
        var result = validator.Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).Distinct().ToList());
    }
}
